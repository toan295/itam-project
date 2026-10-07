using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-03: Quản lý người dùng & phân quyền — chỉ Admin IT.
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin IT")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequestDto> _createValidator;
    private readonly IValidator<UpdateUserRequestDto> _updateValidator;
    private readonly IWebHostEnvironment _environment;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequestDto> createValidator,
        IValidator<UpdateUserRequestDto> updateValidator,
        IWebHostEnvironment environment)
    {
        _userService = userService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await _userService.GetPagedAsync(page, pageSize, search);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _userService.GetByIdAsync(id);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _userService.CreateAsync(dto, GetCurrentUserId());
            RedactSetupTokenIfNotDevelopment(result);
            return CreatedAtAction(nameof(GetById), new { id = result.User.Id },
                ApiResponse<object>.Ok(result, "Tạo người dùng thành công."));
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

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequestDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _userService.UpdateAsync(id, dto, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật người dùng thành công."));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (SelfLockNotAllowedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (LastAdminProtectionException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Admin IT đặt lại mật khẩu HỘ một người dùng khác — không cần người đó tự chứng minh sở
    // hữu email như /auth/forgot-password, vì Admin IT đã xác thực và đứng ra bảo lãnh.
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        try
        {
            var result = await _userService.ResetPasswordAsync(id, GetCurrentUserId());
            RedactSetupTokenIfNotDevelopment(result);
            return Ok(ApiResponse<object>.Ok(result, "Đã tạo liên kết đặt lại mật khẩu cho người dùng."));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (SelfPasswordResetNotAllowedException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private void RedactSetupTokenIfNotDevelopment(UserSetupLinkResponseDto result)
    {
        if (!_environment.IsDevelopment())
        {
            result.DevOnlySetupToken = null;
        }
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
