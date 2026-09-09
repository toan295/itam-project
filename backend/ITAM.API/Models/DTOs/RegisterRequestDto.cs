using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class RegisterRequestDto
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = null!;

    [Required]
    public int RoleId { get; set; }

    [Required]
    public int DepartmentId { get; set; }
}
