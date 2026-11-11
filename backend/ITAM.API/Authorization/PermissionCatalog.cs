namespace ITAM.API.Authorization;

/// <summary>Mã quyền dùng trong [HasPermission(...)]. Thêm quyền mới: khai báo ở đây VÀ trong PermissionCatalog.All.</summary>
public static class Permissions
{
    public const string AssetsCreate = "assets.create";
    public const string AssetsUpdate = "assets.update";
    public const string AssetCategoriesManage = "asset-categories.manage";
    public const string DepartmentsManage = "departments.manage";
    public const string LicensesManage = "licenses.manage";
    public const string AllocationsManage = "allocations.manage";
    public const string EmployeesManage = "employees.manage";
    public const string MaintenanceCreate = "maintenance.create";
    public const string MaintenanceAssign = "maintenance.assign";
    public const string MaintenancePriority = "maintenance.priority";
    public const string MaintenanceUpdateStatus = "maintenance.update-status";
    public const string MaintenanceStats = "maintenance.stats";
    public const string DisposalsInspect = "disposals.inspect";
    public const string DisposalsApprove = "disposals.approve";
    public const string DisposalsExecute = "disposals.execute";
    public const string DisposalStatusesManage = "disposal-statuses.manage";
    public const string LifecycleView = "lifecycle.view";
    public const string ForecastsManage = "forecasts.manage";
    public const string DashboardView = "dashboard.view";
    public const string ReportsExport = "reports.export";
    public const string UsersManage = "users.manage";
    public const string PermissionsManage = "permissions.manage";
    public const string AuditLogsView = "audit-logs.view";
    public const string BackupManage = "backup.manage";
}

/// <param name="SystemOnly">
/// Quyền hệ thống: chỉ Admin IT được giữ (và luôn giữ, không gỡ được) để không ai tự khoá mình ra khỏi hệ thống
/// hoặc trao quyền quản trị cho vai trò khác.
/// </param>
public sealed record PermissionDefinition(string Code, string Group, string Label, string Description, bool SystemOnly = false);

public static class PermissionCatalog
{
    public const string AdminRole = "Admin IT";
    public const string ManagerRole = "Manager";
    public const string TechnicianRole = "Technician";
    public const string EmployeeRole = "Employee";

    /// <summary>Vai trò bị khoá, không chỉnh được: nhân viên chỉ dùng cổng "Tài sản của tôi".</summary>
    public static bool IsLockedRole(string roleName) => roleName == EmployeeRole;

    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        new(Permissions.AssetsCreate, "Tài sản", "Thêm tài sản", "Tạo tài sản mới (Quản lý chỉ trong phòng ban mình)."),
        new(Permissions.AssetsUpdate, "Tài sản", "Sửa tài sản", "Sửa thông tin, đổi trạng thái tài sản."),
        new(Permissions.AssetCategoriesManage, "Tài sản", "Quản lý loại tài sản", "Thêm/sửa/xoá loại tài sản."),
        new(Permissions.DepartmentsManage, "Tài sản", "Quản lý phòng ban", "Thêm/sửa/xoá phòng ban."),
        new(Permissions.LicensesManage, "Phần mềm & Giấy phép", "Quản lý giấy phép", "Xem, thêm/sửa/xoá license và gán cho tài sản."),
        new(Permissions.AllocationsManage, "Phân bổ", "Phân bổ - thu hồi tài sản", "Cấp, thu hồi, xem biên bản bàn giao."),
        new(Permissions.EmployeesManage, "Nhân viên", "Quản lý nhân viên", "Xem và sửa hồ sơ nhân viên sử dụng tài sản (tài khoản đăng nhập của nhân viên tạo ở Quản lý người dùng)."),
        new(Permissions.MaintenanceCreate, "Bảo trì", "Tạo phiếu bảo trì", "Báo lỗi tài sản (nhân viên: chỉ tài sản mình được cấp)."),
        new(Permissions.MaintenanceAssign, "Bảo trì", "Phân công kỹ thuật viên", "Gán hoặc đổi kỹ thuật viên cho phiếu."),
        new(Permissions.MaintenancePriority, "Bảo trì", "Đổi mức độ khẩn", "Điều chỉnh mức độ khẩn của phiếu đang chờ."),
        new(Permissions.MaintenanceUpdateStatus, "Bảo trì", "Cập nhật kết quả xử lý", "Đóng phiếu: đã xử lý / không xử lý được (chỉ phiếu được phân công cho mình)."),
        new(Permissions.MaintenanceStats, "Bảo trì", "Xem thống kê bảo trì", "Thống kê số phiếu, thời gian xử lý trung bình."),
        new(Permissions.DisposalsInspect, "Thanh lý", "Kiểm tra & đề xuất thanh lý", "Kiểm tra tài sản hỏng và đề xuất thanh lý."),
        new(Permissions.DisposalsApprove, "Thanh lý", "Duyệt / từ chối thanh lý", "Duyệt đề xuất thanh lý của phòng ban mình."),
        new(Permissions.DisposalsExecute, "Thanh lý", "Thực hiện thanh lý", "Hoàn tất thanh lý, gán trạng thái phụ."),
        new(Permissions.DisposalStatusesManage, "Thanh lý", "Quản lý trạng thái thanh lý", "Đổi tên, màu, thứ tự các bước của quy trình."),
        new(Permissions.LifecycleView, "Phân tích & dự báo", "Vòng đời tài sản", "Đề xuất thay thế, thống kê tuổi/tỷ lệ lỗi, ngưỡng theo loại."),
        new(Permissions.ForecastsManage, "Phân tích & dự báo", "Dự báo ngân sách", "Kế hoạch mở rộng, định mức, đơn giá, tạo dự báo."),
        new(Permissions.DashboardView, "Phân tích & dự báo", "Xem Dashboard", "Xem số liệu tổng quan và biểu đồ (Quản lý chỉ thấy phòng ban mình; giấy phép và ngân sách cần thêm quyền tương ứng)."),
        new(Permissions.ReportsExport, "Phân tích & dự báo", "Xuất báo cáo Excel/PDF", "Xuất tồn kho, sắp bảo trì, vòng đời, ngân sách (Quản lý chỉ trong phòng ban mình; báo cáo ngân sách cần thêm quyền Dự báo ngân sách)."),
        new(Permissions.UsersManage, "Hệ thống", "Quản lý người dùng", "Tạo/sửa/khoá tài khoản, đặt lại mật khẩu, gán vai trò.", SystemOnly: true),
        new(Permissions.PermissionsManage, "Hệ thống", "Quản lý phân quyền", "Chỉnh ma trận quyền của các vai trò.", SystemOnly: true),
        new(Permissions.AuditLogsView, "Hệ thống", "Xem nhật ký hệ thống", "Tra cứu nhật ký thao tác của mọi người dùng.", SystemOnly: true),
        new(Permissions.BackupManage, "Hệ thống", "Sao lưu & phục hồi dữ liệu", "Tạo, tải về, tải lên bản sao lưu và phục hồi toàn bộ dữ liệu.", SystemOnly: true),
    };

    private static readonly HashSet<string> Known = All.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> System = All.Where(p => p.SystemOnly).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string code) => Known.Contains(code);
    public static bool IsSystemOnly(string code) => System.Contains(code);
    public static IReadOnlySet<string> SystemCodes => System;

    /// <summary>Quyền mặc định của từng vai trò — trùng đúng hành vi phân quyền cố định trước khi có giao diện phân quyền.</summary>
    public static IReadOnlySet<string> DefaultsFor(string roleName) => roleName switch
    {
        // Admin IT cố ý KHÔNG có "kiểm tra/đề xuất" và "duyệt" thanh lý: quy trình tách người đề xuất – người duyệt – người thực hiện.
        AdminRole => All.Select(p => p.Code)
            .Where(c => c is not (Permissions.DisposalsInspect or Permissions.DisposalsApprove))
            .ToHashSet(StringComparer.Ordinal),
        ManagerRole => new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.AssetsCreate, Permissions.AssetsUpdate, Permissions.AllocationsManage, Permissions.EmployeesManage,
            Permissions.MaintenanceCreate, Permissions.MaintenanceAssign, Permissions.MaintenancePriority,
            Permissions.MaintenanceStats, Permissions.DisposalsApprove, Permissions.DashboardView, Permissions.ReportsExport,
        },
        TechnicianRole => new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.MaintenanceUpdateStatus, Permissions.DisposalsInspect,
        },
        EmployeeRole => new HashSet<string>(StringComparer.Ordinal) { Permissions.MaintenanceCreate },
        _ => new HashSet<string>(StringComparer.Ordinal),
    };
}
