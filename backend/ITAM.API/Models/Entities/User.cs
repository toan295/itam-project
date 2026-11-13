namespace ITAM.API.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public int RoleId { get; set; }

    // Tài khoản nhân viên vừa tạo, chờ nhân viên bấm liên kết trong email và tự đặt mật khẩu; chưa đăng nhập được.
    public bool PendingActivation { get; set; }
    public int DepartmentId { get; set; }
    public string? Phone { get; set; }   // Số điện thoại (đã chuẩn hoá, xem PhoneNumber).
    public bool IsActive { get; set; } = true;

    // Xoá tài khoản = xoá mềm: giữ dòng để nhật ký, phiếu bảo trì, biên bản... còn tên người thực hiện. Email được đổi thành giá trị
    // vô hiệu (giải phóng email cho tài khoản mới), mật khẩu bị huỷ, hồ sơ nhân viên được tách ra. Mọi truy vấn người dùng đều bỏ qua dòng này.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // true khi mật khẩu là mật khẩu mặc định do Admin cấp/đặt lại — buộc đổi trước khi dùng hệ thống.
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
