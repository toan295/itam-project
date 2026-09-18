namespace ITAM.API.Models.DTOs.Users;

// Kết quả của "Tạo người dùng" và "Đặt lại mật khẩu hộ" (Admin IT thực hiện) — dùng chung 1 DTO
// vì cả 2 hành động đều kết thúc bằng việc cấp 1 link thiết lập/đặt lại mật khẩu cho người dùng
// đích, Admin không bao giờ biết mật khẩu thật (xem CreateUserRequestDto).
public class UserSetupLinkResponseDto
{
    public UserResponseDto User { get; set; } = default!;

    // Dự án chưa có hạ tầng gửi email — chỉ có giá trị ở môi trường Development để demo/test.
    // Production phải gửi link qua kênh khác (email/SMS) và trường này luôn null.
    public string? DevOnlySetupToken { get; set; }
}
