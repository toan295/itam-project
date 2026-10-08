using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Services.Interfaces;

public interface IDefaultPasswordService
{
    Task<DefaultPasswordDto> GetAsync();
    Task<DefaultPasswordDto> SetAsync(string password, int currentUserId);
    Task ClearAsync(int currentUserId);

    // Dùng cho UserService khi cấp tài khoản/đặt lại mật khẩu; ném DefaultPasswordNotConfiguredException nếu chưa có.
    Task<string> GetRequiredAsync();

    // true nếu candidate trùng mật khẩu mặc định hiện hành — dùng để cấm người dùng "đổi" sang lại mật khẩu mặc định.
    Task<bool> IsDefaultPasswordAsync(string candidate);

    // Nâng cấp giá trị cũ (văn bản thuần) lên dạng mã hoá; gọi một lần lúc khởi động.
    Task EnsureEncryptedAsync();
}
