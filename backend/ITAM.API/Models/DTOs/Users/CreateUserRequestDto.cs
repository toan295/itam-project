namespace ITAM.API.Models.DTOs.Users;

public class CreateUserRequestDto
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int RoleId { get; set; }
    public int DepartmentId { get; set; }

    // Cố ý KHÔNG có trường Password: Admin IT tạo tài khoản nhưng không tự đặt (và do đó không
    // bao giờ biết) mật khẩu của người khác — hệ thống sinh mật khẩu ngẫu nhiên không ai biết,
    // rồi cấp link "thiết lập mật khẩu" (dùng chung cơ chế UC quên mật khẩu) để người dùng tự đặt.
}
