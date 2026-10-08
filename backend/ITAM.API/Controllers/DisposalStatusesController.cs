using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Danh mục trạng thái phiếu thanh lý. Mọi người dùng đăng nhập đọc được (để hiển thị/lọc); chỉ Admin IT thêm/sửa/xoá.
[ApiController]
[Route("api/v1/disposal-statuses")]
[Authorize]
public class DisposalStatusesController : ControllerBase
{
    private readonly IDisposalStatusService _service;
    private readonly IValidator<UpsertDisposalStatusRequestDto> _validator;

    public DisposalStatusesController(IDisposalStatusService service, IValidator<UpsertDisposalStatusRequestDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _service.GetAllAsync()));

    [HttpPost]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Create(UpsertDisposalStatusRequestDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.CreateAsync(dto, GetCurrentUserId());
            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Đã thêm trạng thái."));
        }
        catch (DisposalConflictException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Update(int id, UpsertDisposalStatusRequestDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.UpdateAsync(id, dto, GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, "Đã cập nhật trạng thái."));
        }
        catch (DisposalStatusNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DisposalConflictException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Tạo lại các bước chính mặc định đã bị xoá.
    [HttpPost("restore-defaults")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> RestoreDefaults() =>
        Ok(ApiResponse<object>.Ok(await _service.RestoreDefaultsAsync(GetCurrentUserId()), "Đã khôi phục các bước mặc định."));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (DisposalStatusNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DisposalConflictException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
