using ITAM.API.Filters;
using ITAM.API.Models.Entities;

namespace ITAM.API.Auditing;

/// <summary>Đối tượng + hành động mà một request ghi dữ liệu tác động tới, suy ra từ controller / phương thức / đường dẫn.</summary>
/// <param name="Keys">Các cột khoá (tên thuộc tính thực thể) và tên tham số route chứa giá trị, để đọc bản ghi trước/sau.</param>
/// <param name="ServiceLogsSuccess">Service đã tự ghi sự kiện thành công (trong cùng transaction với nghiệp vụ) — bộ lọc chỉ ghi từ chối/thất bại.</param>
public sealed record AuditTarget(
    string Entity,
    string Action,
    Type? ClrType,
    IReadOnlyList<(string Prop, string Route)> Keys,
    string? EntityIdRoute,
    bool ServiceLogsSuccess);

public static class AuditTargets
{
    private static readonly Dictionary<string, (string Entity, Type? Clr, bool ServiceLogs)> Controllers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Assets"] = ("Asset", typeof(Asset), false),
        ["Departments"] = ("Department", typeof(Department), false),
        ["AssetCategories"] = ("AssetCategory", typeof(AssetCategory), false),
        ["SoftwareLicenses"] = ("SoftwareLicense", typeof(SoftwareLicense), false),
        ["MaintenanceTickets"] = ("MaintenanceTicket", typeof(MaintenanceTicket), false),
        ["AssetAllocations"] = ("AssetAllocation", typeof(AssetAllocation), false),
        ["Employees"] = ("Employee", typeof(Employee), false),
        ["Users"] = ("User", null, true),
        ["DisposalRequests"] = ("DisposalRequest", null, true),
        ["DisposalStatuses"] = ("DisposalStatus", null, true),
    };

    private static readonly IReadOnlyList<(string, string)> ById = new[] { ("Id", "id") };
    private static readonly IReadOnlyList<(string, string)> ByCategory = new[] { ("CategoryId", "categoryId") };
    private static readonly IReadOnlyList<(string, string)> NoKeys = Array.Empty<(string, string)>();

    /// <summary>Trả null nếu request không phải thao tác cần ghi nhật ký (chỉ đọc, hoặc controller không thuộc diện ghi).</summary>
    public static AuditTarget? Resolve(string controller, string httpMethod, IReadOnlyList<string> segments)
    {
        var method = httpMethod.ToUpperInvariant();
        var first = segments.FirstOrDefault(s => !int.TryParse(s, out _));

        switch (controller)
        {
            case "Permissions":
                if (method is not ("PUT" or "POST")) return null;
                return segments.Count > 0 && segments[0] == "roles" && segments.Contains("reset")
                    ? new AuditTarget("Role", "ResetPermissions", null, NoKeys, "roleId", true)
                    : new AuditTarget("Role", "UpdatePermissions", null, NoKeys, "roleId", true);

            case "Auth":
                return method == "POST" && first == "change-password"
                    ? new AuditTarget("User", "ChangePassword", null, NoKeys, null, true)
                    : null;

            case "Reports":   // mọi lần xuất file (kể cả bị từ chối/lỗi) đều để lại dấu vết: ai, loại báo cáo, bộ lọc
                return method == "GET" ? new AuditTarget("Report", "Export", null, NoKeys, null, false) : null;

            // [ĐÃ ĐÓNG]
            // case "BudgetForecasts":
                // return ResolveForecast(method, first);

            case "Lifecycle":
                if (first != "policies" || method is not ("PUT" or "DELETE")) return null;
                return new AuditTarget("LifecyclePolicy", method == "PUT" ? "Upsert" : "Delete",
                    typeof(AssetCategoryLifecyclePolicy), ByCategory, "categoryId", false);
        }

        if (!Controllers.TryGetValue(controller, out var map))
        {
            return null;
        }

        var action = AuditActionFilter.DeriveAction(method, segments);
        return action is null ? null : new AuditTarget(map.Entity, action, map.Clr, ById, "id", map.ServiceLogs);
    }

    // [ĐÃ ĐÓNG]
    // private static AuditTarget? ResolveForecast(string method, string? first)
    // {
    // if (method == "GET") return null;
    // var upsert = method == "PUT" ? "Upsert" : "Delete";
    // return first switch
    // {
    // "generate" when method == "POST" => new AuditTarget("BudgetForecast", "Generate", null, NoKeys, null, false),
    // "reference-prices" => new AuditTarget("ReferencePrice", upsert, typeof(AssetCategoryReferencePrice), ByCategory, "categoryId", false),
    // "device-ratios" => new AuditTarget("DeviceRatio", upsert, typeof(AssetCategoryDeviceRatio), ByCategory, "categoryId", false),
    // "expansion-plans" => new AuditTarget("ExpansionPlan", upsert, typeof(DepartmentExpansionPlan),
    // new[] { ("Year", "year"), ("DepartmentId", "departmentId") }, "departmentId", false),
    // null when method == "DELETE" => new AuditTarget("BudgetForecast", "Delete", typeof(BudgetForecast), ById, "id", false),
    // _ => null,
    // };
    // }
}
