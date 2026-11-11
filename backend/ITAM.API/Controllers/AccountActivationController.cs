using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITAM.API.Controllers;

// Trang công khai cho nhân viên mở liên kết trong email "Kích hoạt tài khoản": xem thông tin tài khoản và tự đặt mật khẩu.
// Xác thực bằng token ngẫu nhiên 256-bit trong URL (dùng một lần, hết hạn sau vài ngày); không cần đăng nhập.
[ApiController]
[Route("api/v1/account-activation")]
[AllowAnonymous]
public class AccountActivationController : ControllerBase
{
    private readonly IAccountActivationService _service;

    public AccountActivationController(IAccountActivationService service)
    {
        _service = service;
    }

    [HttpGet("{token}")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> GetInfo(string token)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await _service.GetPublicInfoAsync(token)));
        }
        catch (ActivationLinkInvalidException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{token}/activate")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Activate(string token, ActivateAccountRequestDto dto)
    {
        try
        {
            var result = await _service.ActivateAsync(token, dto.Password, dto.ConfirmPassword);
            return Ok(ApiResponse<object>.Ok(result, "Tài khoản đã được kích hoạt. Bạn có thể đăng nhập."));
        }
        catch (ActivationLinkInvalidException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ActivationLinkUnusableException ex)
        {
            return StatusCode(StatusCodes.Status410Gone, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
