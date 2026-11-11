namespace ITAM.API.Models.Enums;

// Trạng thái một lần gửi liên kết kích hoạt tài khoản.
public enum ActivationStatus
{
    Pending = 0,      // đã gửi, chờ nhân viên bấm "Kích hoạt tài khoản" và đặt mật khẩu
    Activated = 1,    // nhân viên đã đặt mật khẩu — liên kết dùng một lần, hết hiệu lực
    Superseded = 2,   // bị thay bằng liên kết mới hơn (gửi lại) hoặc bị vô hiệu (email nhân viên đổi)
}
