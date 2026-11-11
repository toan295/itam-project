using ITAM.API.Authorization;
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
[HasPermission(Permissions.UsersManage)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequestDto> _createValidator;
    private readonly IValidator<UpdateUserRequestDto> _updateValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequestDto> createValidator,
        IValidator<UpdateUserRequestDto> updateValidator)
    {
        _userService = userService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] int? roleId = null, [FromQuery] int? departmentId = null, [FromQuery] bool? isActive = null,
        [FromQuery] string? activation = null, [FromQuery] string? sort = null)
    {
        var filter = new UserListFilter
        {
            Search = search, RoleId = roleId, DepartmentId = departmentId, IsActive = isActive, Activation = activation, Sort = sort,
        };
        try
        {
            var result = await _userService.GetPagedAsync(page, pageSize, filter);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
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
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<object>.Ok(result, "Đã tạo tài khoản (chờ kích hoạt). " + (result.ActivationDispatch?.Message ?? "")));
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
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
        catch (SelfRoleChangeNotAllowedException ex)
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

    // Admin IT đặt lại mật khẩu HỘ một người dùng khác: mật khẩu cũ mất hiệu lực ngay, hệ thống gửi liên kết để người đó tự đặt mật khẩu mới.
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        try
        {
            var result = await _userService.ResetPasswordAsync(id, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, "Đã vô hiệu mật khẩu cũ. " + (result.ActivationDispatch?.Message ?? "")));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (SelfPasswordResetNotAllowedException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ActivationResendTooSoonException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Xoá tài khoản (xoá mềm, giữ lịch sử). 204 khi thành công.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _userService.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (SelfDeleteNotAllowedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (LastAdminProtectionException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UserHasPendingWorkException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Nhân viên đang hoạt động chưa có tài khoản — để chọn khi tạo tài khoản vai trò Nhân viên.
    [HttpGet("employee-candidates")]
    public async Task<IActionResult> GetEmployeeCandidates([FromQuery] string? keyword = null) =>
        Ok(ApiResponse<object>.Ok(await _userService.GetEmployeeCandidatesAsync(keyword)));

    // Gửi lại email kích hoạt cho tài khoản nhân viên đang chờ kích hoạt.
    [HttpPost("{id:int}/resend-activation")]
    public async Task<IActionResult> ResendActivation(int id)
    {
        try
        {
            var result = await _userService.ResendActivationAsync(id, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, result.ActivationDispatch?.Message ?? "Đã xử lý yêu cầu gửi lại email kích hoạt."));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ActivationResendTooSoonException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
