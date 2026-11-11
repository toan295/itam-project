namespace ITAM.API.Models.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Role { get; set; } = null!;
    public int DepartmentId { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    /// <summary>Mã quyền của vai trò hiện tại (ma trận phân quyền) — để giao diện ẩn/hiện chức năng.</summary>
    public List<string> Permissions { get; set; } = new();
}
