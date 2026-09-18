using ITAM.API.Models.DTOs;

namespace ITAM.API.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);
    Task<UserProfileDto?> GetMeAsync(int userId);
    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto dto);
    Task ResetPasswordAsync(ResetPasswordRequestDto dto);
    Task ChangePasswordAsync(int userId, ChangePasswordRequestDto dto);
}
