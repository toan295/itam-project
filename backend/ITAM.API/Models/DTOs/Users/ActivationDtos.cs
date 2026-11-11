using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs.Users;

// Kết quả của lần gửi email vừa thực hiện (tạo/gửi lại/đặt lại mật khẩu).
// Mode: smtp | outbox (Development, chưa gửi thật) | disabled (chưa cấu hình SMTP).
public class EmailDispatchDto
{
    public bool Sent { get; set; }
    public string Mode { get; set; } = "disabled";
    public string Message { get; set; } = "";
}

// Trạng thái kích hoạt của một tài khoản nhân viên (suy ra từ lần gửi liên kết mới nhất).
// Status: None (không áp dụng / không có lần gửi nào) | Pending | Expired (Pending nhưng quá hạn) | Activated.
public class ActivationStatusDto
{
    public string Status { get; set; } = "None";
    public string? EmailTo { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? SendError { get; set; }
}

// Thông tin trả cho trang kích hoạt công khai (nhân viên mở liên kết trong email).
// Status: Pending | Expired | Activated | Superseded.
public class ActivationPublicInfoDto
{
    public string FullName { get; set; } = default!;
    public string? EmployeeCode { get; set; }
    public string Email { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public string DepartmentName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }

    // "Activate" (tài khoản mới) | "Reset" (Admin đặt lại mật khẩu) — trang công khai đổi chữ cho phù hợp.
    public string Purpose { get; set; } = "Activate";
}

public class ActivateAccountRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."),
     MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự."),
     MaxLength(72, ErrorMessage = "Mật khẩu không được vượt quá 72 ký tự.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    public string ConfirmPassword { get; set; } = null!;
}

// Nhân viên chưa có tài khoản — nguồn chọn khi tạo tài khoản vai trò Nhân viên.
public class EmployeeCandidateDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
}
