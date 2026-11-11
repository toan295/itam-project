namespace ITAM.API.Models.DTOs.Employees;

// Bộ lọc danh sách nhân viên. AccountState: none | pending | active | locked (giống huy hiệu ở cột "Tài khoản").
// RoleName: vai trò của tài khoản gắn với nhân viên. HoldingAssets: đang giữ (true) hay không giữ (false) tài sản nào.
// Sort: name (mặc định) | code | department | newest.
public class EmployeeListFilter
{
    public static readonly string[] AccountStates = { "none", "pending", "active", "locked" };
    public static readonly string[] RoleNames = { "Admin IT", "Manager", "Technician", "Employee" };
    public static readonly string[] Sorts = { "name", "code", "department", "newest" };

    public int? DepartmentId { get; set; }
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public string? AccountState { get; set; }
    public string? RoleName { get; set; }
    public bool? HoldingAssets { get; set; }
    public string? Sort { get; set; }

    // Manager: ẩn hồ sơ gắn tài khoản Admin IT / Manager / Technician. Do Service đặt, không nhận từ client.
    public bool ExcludeStaffAccounts { get; set; }
}
