using System.Security.Claims;
using ITAM.API.Models.DTOs;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<AuthResponseDto>.Ok(result, "Đăng ký thành công."));
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập thành công."));
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AccountLockedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Quên mật khẩu: luôn trả 200 với message chung dù email có tồn tại hay không (chống dò email).
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        var result = await _authService.ForgotPasswordAsync(dto);

        // Dự án chưa có hạ tầng gửi email — chỉ để lộ token qua response ở môi trường Development
        // để tiện demo/test. Production phải gửi qua email, KHÔNG bao giờ trả token trong response.
        if (!_environment.IsDevelopment())
        {
            result.DevOnlyResetToken = null;
        }

        return Ok(ApiResponse<ForgotPasswordResponseDto>.Ok(result, result.Message));
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
            return Ok(ApiResponse<object>.Ok(new { }, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại."));
        }
        catch (InvalidResetTokenException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Đổi mật khẩu khi đang đăng nhập và còn nhớ mật khẩu hiện tại — khác với quên mật khẩu
    // (không cần token, chỉ cần xác nhận đúng mật khẩu cũ). Áp dụng như nhau cho mọi role.
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await _authService.ChangePasswordAsync(userId, dto);
            return Ok(ApiResponse<object>.Ok(new { }, "Đổi mật khẩu thành công."));
        }
        catch (InvalidCredentialsException)
        {
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu hiện tại không đúng."));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = int.Parse(userIdClaim!);

        var profile = await _authService.GetMeAsync(userId);
        if (profile is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy người dùng."));
        }

        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }
}
