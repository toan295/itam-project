using System.Security.Claims;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Users;
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
    private readonly IPermissionService _permissionService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IPermissionService permissionService, IUserService userService)
    {
        _authService = authService;
        _permissionService = permissionService;
        _userService = userService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
            result.Permissions = await _permissionService.GetPermissionsForRoleAsync(result.Role);
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
        catch (TooManyLoginAttemptsException ex)
        {
            Response.Headers.RetryAfter = ((int)Math.Ceiling(ex.RetryAfter.TotalSeconds)).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Ghi nhận đăng xuất vào nhật ký (token JWT vẫn hết hạn tự nhiên; client xoá token ở phía mình).
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đăng xuất."));
    }

    // Đổi mật khẩu khi đang đăng nhập, cần xác nhận đúng mật khẩu hiện tại. Áp dụng như nhau cho mọi role.
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
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
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
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

        profile.Permissions = await _permissionService.GetPermissionsForRoleAsync(profile.Role);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    // Mọi tài khoản tự sửa họ tên / số điện thoại của mình (email, phòng ban, vai trò do Admin IT quản lý).
    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateOwnProfileRequestDto dto)
    {
        if (dto.FullName is not null && (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length > 100))
        {
            return BadRequest(ApiResponse<object>.Fail("Họ tên không được để trống và tối đa 100 ký tự."));
        }

        if (!string.IsNullOrWhiteSpace(dto.Phone) && !ITAM.API.Helpers.PhoneNumber.IsValid(dto.Phone))
        {
            return BadRequest(ApiResponse<object>.Fail(ITAM.API.Helpers.PhoneNumber.InvalidMessage));
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var result = await _userService.UpdateOwnProfileAsync(userId, dto.FullName, dto.Phone);
            return Ok(ApiResponse<object>.Ok(result, "Đã cập nhật thông tin cá nhân."));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
