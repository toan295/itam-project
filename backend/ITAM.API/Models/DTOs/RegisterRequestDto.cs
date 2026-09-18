using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [MaxLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Phải chọn phòng ban.")]
    public int DepartmentId { get; set; }
}
