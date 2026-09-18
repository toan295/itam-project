namespace ITAM.API.Models.DTOs.Users;

public class UserResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
