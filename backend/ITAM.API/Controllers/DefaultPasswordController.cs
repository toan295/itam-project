using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Admin IT quản lý mật khẩu mặc định dùng khi cấp tài khoản/đặt lại mật khẩu (UC-03).
[ApiController]
[Route("api/v1/settings/default-password")]
[Authorize(Roles = "Admin IT")]
public class DefaultPasswordController : ControllerBase
{
    private readonly IDefaultPasswordService _service;
    private readonly IValidator<SetDefaultPasswordRequestDto> _validator;

    public DefaultPasswordController(IDefaultPasswordService service, IValidator<SetDefaultPasswordRequestDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(ApiResponse<DefaultPasswordDto>.Ok(await _service.GetAsync()));

    // PUT dùng cho cả "thêm" lần đầu và "sửa" — idempotent.
    [HttpPut]
    public async Task<IActionResult> Set(SetDefaultPasswordRequestDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.SetAsync(dto.Password, GetCurrentUserId());
            return Ok(ApiResponse<DefaultPasswordDto>.Ok(result, "Đã lưu mật khẩu mặc định."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _service.ClearAsync(GetCurrentUserId());
        return NoContent();
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
