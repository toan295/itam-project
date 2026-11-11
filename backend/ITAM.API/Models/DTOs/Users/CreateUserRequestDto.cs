namespace ITAM.API.Models.DTOs.Users;

// Mọi tài khoản đăng nhập đều gắn với MỘT nhân viên có sẵn trong Danh mục nhân viên (quan hệ 1-1): họ tên, email, số điện thoại,
// phòng ban lấy từ hồ sơ nhân viên (chỉnh ở Danh mục nhân viên). Ở đây chỉ chọn nhân viên và vai trò.
// Cố ý KHÔNG có trường mật khẩu: người dùng tự đặt mật khẩu qua liên kết kích hoạt trong email.
public class CreateUserRequestDto
{
    public int RoleId { get; set; }
    public int EmployeeId { get; set; }
}
