using System.Security.Claims;
using System.Text.Json;
using ITAM.API.Data;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ITAM.API.Filters;

// Ghi nhật ký hệ thống tự động cho các thao tác GHI thành công (POST/PUT/PATCH/DELETE) và việc IN biên bản
// (GET .../document) của các module CRUD — để mọi hành động (thêm, sửa, xoá, gán, thu hồi, tạo phiếu bảo trì,
// in biên bản...) đều có dấu vết mà không phải sửa từng Service.
// KHÔNG áp dụng cho: Users, Disposal*, DefaultPassword (Service tự ghi kèm giá trị cũ/mới), Auth (ghi riêng),
// Forecasts/Lifecycle (ngoài phạm vi), và mọi thao tác thất bại (4xx/5xx) hay chỉ đọc.
public class AuditActionFilter : IAsyncActionFilter
{
    // Tên controller -> tên thực thể ghi trong nhật ký.
    private static readonly Dictionary<string, string> EntityByController = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Assets"] = "Asset",
        ["Departments"] = "Department",
        ["AssetCategories"] = "AssetCategory",
        ["SoftwareLicenses"] = "SoftwareLicense",
        ["MaintenanceTickets"] = "MaintenanceTicket",
        ["AssetAllocations"] = "AssetAllocation",
        ["Employees"] = "Employee",
    };

    private static readonly Dictionary<string, string> VerbActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assign"] = "Assign",
        ["status"] = "UpdateStatus",
        ["priority"] = "UpdatePriority",
        ["return"] = "Return",
        ["document"] = "Print",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(AppDbContext db, IAuditLogService auditLog, ILogger<AuditActionFilter> logger)
    {
        _db = db;
        _auditLog = auditLog;
        _logger = logger;
    }

    // Suy ra tên hành động từ phương thức HTTP và các đoạn đường dẫn SAU tên tài nguyên (vd ["5","status"]).
    public static string? DeriveAction(string httpMethod, IReadOnlyList<string> segmentsAfterResource)
    {
        var verb = segmentsAfterResource.FirstOrDefault(s => !int.TryParse(s, out _));
        var method = httpMethod.ToUpperInvariant();

        if (method == "GET")
        {
            return verb is not null && verb.Equals("document", StringComparison.OrdinalIgnoreCase) ? "Print" : null;
        }

        if (verb is not null)
        {
            var name = VerbActions.TryGetValue(verb, out var mapped) ? mapped : ToPascalCase(verb);
            return method == "DELETE" ? "Un" + name.ToLowerInvariant() : name;
        }

        return method switch
        {
            "POST" => "Create",
            "PUT" or "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => null,
        };
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        try
        {
            await TryRecordAsync(context, executed);
        }
        catch (Exception ex)
        {
            // Nhật ký không được làm hỏng thao tác nghiệp vụ đã thành công.
            _logger.LogWarning(ex, "Không ghi được nhật ký tự động cho {Path}", context.HttpContext.Request.Path);
        }
    }

    private async Task TryRecordAsync(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        if (context.RouteData.Values["controller"] is not string controller
            || !EntityByController.TryGetValue(controller, out var entityName))
        {
            return;
        }

        var status = executed.Result switch
        {
            ObjectResult o => o.StatusCode ?? 200,
            StatusCodeResult s => s.StatusCode,
            _ => 200,
        };
        if (status is < 200 or >= 300)
        {
            return;
        }

        if (!int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var segments = GetSegmentsAfterResource(context.HttpContext.Request.Path.Value, controller);
        var action = DeriveAction(context.HttpContext.Request.Method, segments);
        if (action is null)
        {
            return;
        }

        var entityId = ResolveEntityId(context, executed);
        await _auditLog.RecordAsync(userId, action, entityName, entityId, null, BuildPayload(context));
        await _db.SaveChangesAsync();
    }

    // "/api/v1/maintenance-tickets/5/status" -> ["5","status"]; bỏ tiền tố /api/v1/{tài-nguyên}.
    private static IReadOnlyList<string> GetSegmentsAfterResource(string? path, string controller)
    {
        var parts = (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        var idx = Array.FindIndex(parts, p => p.Replace("-", "").Equals(controller.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            // Tên route (kebab-case) khác tên controller (vd allocations vs AssetAllocations) -> lấy sau "v1/{resource}".
            idx = Array.FindIndex(parts, p => p.Equals("v1", StringComparison.OrdinalIgnoreCase)) + 1;
        }

        return idx < 0 || idx + 1 >= parts.Length ? Array.Empty<string>() : parts[(idx + 1)..];
    }

    private static int ResolveEntityId(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (context.RouteData.Values["id"] is { } routeId && int.TryParse(routeId.ToString(), out var id))
        {
            return id;
        }

        // POST tạo mới: lấy Id từ response ApiResponse<...>.Data.Id.
        if (executed.Result is ObjectResult { Value: { } value })
        {
            var data = value.GetType().GetProperty("Data")?.GetValue(value);
            if (data?.GetType().GetProperty("Id")?.GetValue(data) is int created)
            {
                return created;
            }
        }

        return 0;
    }

    // Nội dung yêu cầu (DTO) làm "giá trị mới"; các module trong danh sách không có DTO chứa mật khẩu/bí mật.
    private static object? BuildPayload(ActionExecutingContext context)
    {
        var values = new Dictionary<string, object?>();

        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null || arg is string || arg.GetType().IsPrimitive || arg is CancellationToken)
            {
                continue;
            }

            values["request"] = JsonSerializer.SerializeToElement(arg, JsonOptions);
        }

        foreach (var key in new[] { "assetId", "kind" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var rv) && rv is not null)
            {
                values[key] = rv.ToString();
            }
            else if (context.HttpContext.Request.Query.TryGetValue(key, out var qv) && qv.Count > 0)
            {
                values[key] = qv.ToString();
            }
        }

        return values.Count == 0 ? null : values;
    }

    private static string ToPascalCase(string kebab) =>
        string.Concat(kebab.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
}
