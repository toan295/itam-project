namespace ITAM.API.Models.DTOs.Users;

public class UserResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }

    // Tài khoản của nhân viên nhận tài sản (vai trò Employee): tạo từ một nhân viên có sẵn, thông tin sửa ở Danh mục nhân viên.
    public bool IsEmployeeAccount { get; set; }

    // Chờ người dùng bấm liên kết trong email và tự đặt mật khẩu (tài khoản mới hoặc vừa được Admin đặt lại mật khẩu); chưa đăng nhập được.
    public bool PendingActivation { get; set; }

    // Cảnh báo không chặn thao tác (vd số kỹ thuật viên đang hoạt động xuống dưới mức tối thiểu); chỉ có khi cập nhật.
    public string? Warning { get; set; }
    public DateTime CreatedAt { get; set; }

    // Trạng thái kích hoạt / đặt mật khẩu (lần gửi liên kết mới nhất) và kết quả lần gửi email vừa thực hiện.
    public ActivationStatusDto Activation { get; set; } = new();
    public EmailDispatchDto? ActivationDispatch { get; set; }
}
