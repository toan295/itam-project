namespace ITAM.API.Models.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public int DepartmentId { get; set; }
    public bool IsActive { get; set; }
}
