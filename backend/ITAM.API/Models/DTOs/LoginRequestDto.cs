using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MaxLength(72)] // chặn payload khổng lồ đẩy vào BCrypt (tốn CPU).
    public string Password { get; set; } = null!;
}
