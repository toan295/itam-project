using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

// Một lần gửi liên kết "Kích hoạt tài khoản" cho tài khoản nhân viên: người nhận bấm liên kết, tự đặt mật khẩu và tài khoản mới
// dùng được. Chỉ lưu băm SHA-256 của token (token gốc chỉ có trong email). Lần gửi MỚI NHẤT quyết định trạng thái hiển thị.
public class AccountActivation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string EmailTo { get; set; } = null!;          // địa chỉ đã gửi tới (email nhân viên có thể đổi sau này)
    public string Reason { get; set; } = null!;           // Created | Resent
    public ActivationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? SentAt { get; set; }                 // null = chưa gửi được (xem SendError)
    public string? SendError { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public User User { get; set; } = null!;
}
