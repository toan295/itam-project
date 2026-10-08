using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class ChangePasswordRequestDto
{
    [Required, MaxLength(72)]
    public string CurrentPassword { get; set; } = null!;

    [Required, MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự."), MaxLength(72, ErrorMessage = "Mật khẩu không được vượt quá 72 ký tự.")]
    public string NewPassword { get; set; } = null!;
}
