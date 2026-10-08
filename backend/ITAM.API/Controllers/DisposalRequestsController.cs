using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Luồng thanh lý: Technician kiểm tra -> đề xuất -> Manager duyệt -> Admin IT thực hiện -> hoàn tất.
[ApiController]
[Route("api/v1/disposal-requests")]
[Authorize]
public class DisposalRequestsController : ControllerBase
{
    private readonly IDisposalRequestService _service;
    private readonly IValidator<CreateDisposalRequestDto> _createValidator;
    private readonly IValidator<ProposeDisposalRequestDto> _proposeValidator;
    private readonly IValidator<ReviewDisposalRequestDto> _reviewValidator;
    private readonly IValidator<CompleteDisposalRequestDto> _completeValidator;

    public DisposalRequestsController(
        IDisposalRequestService service,
        IValidator<CreateDisposalRequestDto> createValidator,
        IValidator<ProposeDisposalRequestDto> proposeValidator,
        IValidator<ReviewDisposalRequestDto> reviewValidator,
        IValidator<CompleteDisposalRequestDto> completeValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _proposeValidator = proposeValidator;
        _reviewValidator = reviewValidator;
        _completeValidator = completeValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId, [FromQuery] int? assetId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetPagedAsync(
            departmentId, assetId, status, page, pageSize, GetRole(), GetDepartmentId(), GetUserId());
        return Ok(ApiResponse<object>.Ok(result));
    }

    // Tài sản Hỏng chưa có phiếu thanh lý đang mở.
    [HttpGet("candidates")]
    public async Task<IActionResult> GetCandidates([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(ApiResponse<object>.Ok(await _service.GetCandidatesAsync(page, pageSize, GetRole(), GetDepartmentId())));

    [HttpGet("{id:int}")]
    public Task<IActionResult> GetById(int id) =>
        Run(async () => Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, GetRole(), GetDepartmentId(), GetUserId()))));

    // Bước 1: Technician kiểm tra tài sản.
    [HttpPost]
    [Authorize(Roles = "Technician")]
    public async Task<IActionResult> Create(CreateDisposalRequestDto dto)
    {
        var bad = await ValidateAsync(_createValidator, dto);
        if (bad is not null) return bad;

        return await Run(async () =>
        {
            var result = await _service.CreateAsync(dto, GetUserId());
            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(result, "Đã ghi nhận kết quả kiểm tra."));
        });
    }

    // Bước 2: Technician đề xuất thanh lý.
    [HttpPost("{id:int}/propose")]
    [Authorize(Roles = "Technician")]
    public async Task<IActionResult> Propose(int id, ProposeDisposalRequestDto dto)
    {
        var bad = await ValidateAsync(_proposeValidator, dto);
        if (bad is not null) return bad;

        return await Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.ProposeAsync(id, dto, GetUserId()), "Đã gửi đề xuất thanh lý, chờ Manager duyệt.")));
    }

    // Bước 3: Manager duyệt / từ chối (chỉ phiếu của phòng ban mình).
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Approve(int id, ReviewDisposalRequestDto dto)
    {
        var bad = await ValidateAsync(_reviewValidator, dto);
        if (bad is not null) return bad;

        return await Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, dto, GetUserId(), GetDepartmentId()), "Đã duyệt đề xuất thanh lý.")));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Reject(int id, ReviewDisposalRequestDto dto)
    {
        var bad = await ValidateAsync(_reviewValidator, dto);
        if (bad is not null) return bad;

        return await Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.RejectAsync(id, dto, GetUserId(), GetDepartmentId()), "Đã từ chối đề xuất thanh lý.")));
    }

    // Bước 4: Admin IT thực hiện thanh lý.
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Complete(int id, CompleteDisposalRequestDto dto)
    {
        var bad = await ValidateAsync(_completeValidator, dto);
        if (bad is not null) return bad;

        return await Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.CompleteAsync(id, dto, GetUserId()), "Đã thanh lý tài sản. Phiếu hoàn tất.")));
    }

    [HttpPatch("{id:int}/sub-status")]
    [Authorize(Roles = "Admin IT")]
    public Task<IActionResult> SetSubStatus(int id, SetDisposalSubStatusRequestDto dto) =>
        Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.SetSubStatusAsync(id, dto.SubStatusId, GetUserId()), "Đã cập nhật trạng thái phụ.")));

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T dto)
    {
        var validation = await validator.ValidateAsync(dto);
        return validation.IsValid
            ? null
            : BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    // Map exception -> status code tập trung (chưa có Exception Handling Middleware toàn cục).
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is DisposalRequestNotFoundException or DisposalStatusNotFoundException or AssetNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex) when (ex is DisposalConflictException or AssetHasOpenAllocationException)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DisposalForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private string? GetRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId) ? departmentId : null;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
