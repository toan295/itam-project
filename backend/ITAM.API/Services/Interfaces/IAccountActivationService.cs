using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Services.Interfaces;

public interface IAccountActivationService
{
    public const string ReasonCreated = "Created";
    public const string ReasonResent = "Resent";
    public const string ReasonReset = "Reset";   // Admin đặt lại mật khẩu: mật khẩu cũ đã vô hiệu, gửi liên kết đặt mật khẩu mới

    // Tạo liên kết kích hoạt / đặt mật khẩu (vô hiệu liên kết cũ) và gửi email. Tài khoản phải đang chờ kích hoạt.
    Task<EmailDispatchDto> SendAsync(int userId, string reason);

    Task<Dictionary<int, ActivationStatusDto>> GetStatusesAsync(IEnumerable<int> userIds);

    // Đánh dấu mọi liên kết còn Pending của tài khoản là Superseded (vd email nhân viên đổi: liên kết đã gửi tới địa chỉ cũ
    // không được dùng nữa). KHÔNG tự lưu — người gọi SaveChanges cùng thay đổi của mình (cùng DbContext).
    Task InvalidatePendingAsync(int userId);

    // Luồng công khai (người nhận bấm liên kết trong email).
    Task<ActivationPublicInfoDto> GetPublicInfoAsync(string token);
    Task<ActivationPublicInfoDto> ActivateAsync(string token, string password, string confirmPassword);
}
