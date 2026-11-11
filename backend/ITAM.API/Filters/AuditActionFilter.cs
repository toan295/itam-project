using System.Security.Claims;
using System.Text.Json;
using ITAM.API.Auditing;
using ITAM.API.Models.Entities;
using ITAM.API.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Filters;

// Ghi nhật ký hệ thống tự động cho MỌI thao tác ghi (POST/PUT/PATCH/DELETE) và việc IN biên bản, kèm KẾT QUẢ:
//  - Success: nghiệp vụ đã thực hiện (giá trị cũ/mới = các trường thực sự đổi);
//  - Rejected: bị từ chối (dữ liệu sai, không tìm thấy, xung đột, không đủ quyền...) — KHÔNG có thay đổi dữ liệu;
//  - Failed: lỗi hệ thống, thao tác không hoàn tất.
// Controller mà Service đã tự ghi sự kiện thành công (Users, Disposal*, Permissions, đổi mật khẩu) thì bộ lọc chỉ ghi
// Rejected/Failed. Sự kiện được ghi bằng DbContext riêng nên vẫn lưu được kể cả khi giao dịch nghiệp vụ đã bị hoàn lại.
public class AuditActionFilter : IAsyncActionFilter, IOrderedFilter
{
    // Chạy TRƯỚC bộ lọc ModelState tự động của [ApiController] (-2000) để cả yêu cầu bị từ chối vì dữ liệu sai (400) cũng được ghi.
    public const int FilterOrder = -3000;

    private static readonly Dictionary<string, string> VerbActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assign"] = "Assign",
        ["status"] = "UpdateStatus",
        ["priority"] = "UpdatePriority",
        ["return"] = "Return",
        ["document"] = "Print",
        ["sub-status"] = "SetSubStatus",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AuditSnapshotReader _reader;
    private readonly AuditEventWriter _writer;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(AuditSnapshotReader reader, AuditEventWriter writer, ILogger<AuditActionFilter> logger)
    {
        _reader = reader;
        _writer = writer;
        _logger = logger;
    }

    public int Order => FilterOrder;

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

    // "/api/v1/maintenance-tickets/5/status" -> ["5","status"]; bỏ tiền tố /api/v1/{tài-nguyên}.
    public static IReadOnlyList<string> GetSegmentsAfterResource(string? path, string controller)
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

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var target = ResolveTarget(context);
        if (target is null || !int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await next();
            return;
        }

        // Bản ghi TRƯỚC thao tác (sửa/xoá/đổi trạng thái...). Tạo mới thì chưa có gì để so sánh.
        Dictionary<string, object?>? before = null;
        if (!target.ServiceLogsSuccess && target.ClrType is not null && !IsCreate(context.HttpContext.Request.Method, target))
        {
            try
            {
                before = await _reader.ReadAsync(target, key => RouteValue(context, key));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được giá trị cũ cho nhật ký {Path}", context.HttpContext.Request.Path);
            }
        }

        var executed = await next();

        try
        {
            await RecordAsync(context, executed, target, userId, before);
        }
        catch (Exception ex)
        {
            // Nhật ký không được làm hỏng thao tác nghiệp vụ đã thành công.
            _logger.LogWarning(ex, "Không ghi được nhật ký tự động cho {Path}", context.HttpContext.Request.Path);
        }
    }

    private static AuditTarget? ResolveTarget(ActionExecutingContext context)
    {
        if (context.RouteData.Values["controller"] is not string controller)
        {
            return null;
        }

        var segments = GetSegmentsAfterResource(context.HttpContext.Request.Path.Value, controller);
        return AuditTargets.Resolve(controller, context.HttpContext.Request.Method, segments);
    }

    private static bool IsCreate(string method, AuditTarget target) =>
        target.Action == "Create" && method.Equals("POST", StringComparison.OrdinalIgnoreCase);

    private static string? RouteValue(ActionExecutingContext context, string key) => context.RouteData.Values[key]?.ToString();

    private async Task RecordAsync(ActionExecutingContext context, ActionExecutedContext executed, AuditTarget target, int userId, Dictionary<string, object?>? before)
    {
        var (outcome, status, message) = Classify(executed);
        if (outcome == AuditOutcome.Success && target.ServiceLogsSuccess)
        {
            return; // Service đã ghi sự kiện thành công cùng transaction với nghiệp vụ.
        }

        var entityId = ResolveEntityId(context, executed, target, userId);
        object? oldValue = null;
        object? newValue = null;
        if (outcome == AuditOutcome.Success && target.ClrType is not null)
        {
            var idRoute = target.Keys.Count > 0 ? target.Keys[0].Route : null;
            var after = await _reader.ReadAsync(target, key => RouteValue(context, key) ?? (key == idRoute && entityId > 0 ? entityId.ToString() : null));
            (oldValue, newValue) = AuditDiff.Compute(before, after);
            if (oldValue is null && newValue is null && target.Action is not ("Print" or "Generate" or "Create"))
            {
                message = "Không có thay đổi dữ liệu (dữ liệu đã đúng như yêu cầu).";
            }
        }

        await _writer.WriteAsync(new AuditEvent(
            userId, target.Action, target.Entity, entityId, outcome, oldValue, newValue, status, message, BuildDetail(context)));
    }

    // Kết quả cuối cùng của yêu cầu: 2xx = Success; 4xx = Rejected (bị từ chối, KHÔNG đổi dữ liệu); 5xx/ngoại lệ = Failed.
    public static (string Outcome, int Status, string? Message) Classify(ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return executed.Exception is ConcurrencyConflictException or DbUpdateConcurrencyException
                ? (AuditOutcome.Rejected, 409, ConcurrencyConflictException.DefaultMessage)
                : (AuditOutcome.Failed, 500, $"Lỗi hệ thống, thao tác không hoàn tất ({executed.Exception.GetType().Name}).");
        }

        var status = executed.Result switch
        {
            ObjectResult o => o.StatusCode ?? 200,
            StatusCodeResult s => s.StatusCode,
            _ => 200,
        };
        var outcome = status switch
        {
            >= 200 and < 300 => AuditOutcome.Success,
            >= 400 and < 500 => AuditOutcome.Rejected,
            _ => AuditOutcome.Failed,
        };
        return (outcome, status, outcome == AuditOutcome.Success ? null : ExtractMessage(executed.Result) ?? $"Yêu cầu không được chấp nhận (HTTP {status}).");
    }

    // Lý do bị từ chối lấy từ ApiResponse { message, errors } mà controller/validator đã trả cho người dùng.
    private static string? ExtractMessage(IActionResult? result)
    {
        if (result is not ObjectResult { Value: { } value })
        {
            return null;
        }

        var type = value.GetType();
        var message = type.GetProperty("Message")?.GetValue(value) as string;
        var errors = (type.GetProperty("Errors")?.GetValue(value) as IEnumerable<string>)?.Take(5).ToList();
        var text = errors is { Count: > 0 } ? $"{message} {string.Join("; ", errors)}".Trim() : message;
        return text is { Length: > 500 } ? text[..500] : text;
    }

    private static int ResolveEntityId(ActionExecutingContext context, ActionExecutedContext executed, AuditTarget target, int userId)
    {
        if (target.EntityIdRoute is null)
        {
            return target.Entity == "User" ? userId : 0;
        }

        if (context.RouteData.Values[target.EntityIdRoute] is { } routeId && int.TryParse(routeId.ToString(), out var id))
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

    // Yêu cầu người dùng đã gửi (đã che mật khẩu/token). Đây là "họ đã xin gì", KHÁC với giá trị cũ/mới là dữ liệu thực sự đổi.
    private static object? BuildDetail(ActionExecutingContext context)
    {
        var values = new Dictionary<string, object?>();

        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is IFormFile upload)
            {
                values["file"] = new { name = Path.GetFileName(upload.FileName), upload.Length };   // chỉ tên + kích thước, không đọc nội dung
                continue;
            }

            if (arg is null || arg is string || arg.GetType().IsPrimitive || arg is CancellationToken)
            {
                continue;
            }

            values["request"] = AuditRedactor.Redact(JsonSerializer.SerializeToElement(arg, JsonOptions));
        }

        if (context.RouteData.Values["controller"] is "Reports")
        {
            values["report"] = context.HttpContext.Request.Path.Value;   // báo cáo nào (tồn kho, vòng đời...) — bộ lọc nằm ở các khoá bên dưới
        }

        foreach (var key in new[] { "assetId", "kind", "reason", "year", "categoryId", "departmentId", "roleId", "format", "status", "purchaseYear", "months", "days" })
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
