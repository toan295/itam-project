namespace ITAM.API.Models.DTOs.Users;

// Bộ lọc danh sách người dùng. IsActive=false là tài khoản đã khoá. Activation: pending (chờ kích hoạt) | activated.
// Sort: role (mặc định, nhóm theo vai trò) | name | newest.
public class UserListFilter
{
    public static readonly string[] Activations = { "pending", "activated" };
    public static readonly string[] Sorts = { "role", "name", "newest" };

    public string? Search { get; set; }
    public int? RoleId { get; set; }
    public int? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public string? Activation { get; set; }
    public string? Sort { get; set; }
}
