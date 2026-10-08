using ITAM.API.Models.DTOs;

namespace ITAM.API.Services.Interfaces;

public interface IAuthService
{
    // clientIp: địa chỉ máy gọi, dùng để chặn dò mật khẩu theo từng cặp (IP, email).
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string? clientIp = null);
    Task<UserProfileDto?> GetMeAsync(int userId);
    Task LogoutAsync(int userId);
    Task ChangePasswordAsync(int userId, ChangePasswordRequestDto dto);
}
