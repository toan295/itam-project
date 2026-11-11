using ITAM.API.Authorization;
using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/allocations")]
[Authorize]
public class AssetAllocationsController : ControllerBase
{
    private readonly IAssetAllocationService _service;
    private readonly IValidator<CreateAssetAllocationDto> _createValidator;
    private readonly IValidator<ReturnAssetAllocationDto> _returnValidator;

    public AssetAllocationsController(
        IAssetAllocationService service,
        IValidator<CreateAssetAllocationDto> createValidator,
        IValidator<ReturnAssetAllocationDto> returnValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _returnValidator = returnValidator;
    }

    [HttpPost]
    [HasPermission(Permissions.AllocationsManage)]
    public async Task<IActionResult> Create(CreateAssetAllocationDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.",
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.CreateAsync(
                dto,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId(),
                GetCurrentUserId());

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                ApiResponse<object>.Ok(result, "Phân bổ tài sản thành công."));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetNotAvailableForAllocationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetAlreadyAllocatedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:int}/return")]
    [HasPermission(Permissions.AllocationsManage)]
    public async Task<IActionResult> Return(int id, ReturnAssetAllocationDto dto)
    {
        var validation = await _returnValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.",
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _service.ReturnAsync(
                id,
                dto,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId(),
                GetCurrentUserId());

            return Ok(ApiResponse<object>.Ok(result, "Thu hồi tài sản thành công."));
        }
        catch (AllocationNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (AllocationAlreadyReturnedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet]
    [HasPermission(Permissions.AllocationsManage)] // UC-14/UC-15: tác nhân chỉ gồm Admin IT, Manager.
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] int? employeeId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] DateOnly? allocatedFrom = null,
        [FromQuery] DateOnly? allocatedTo = null,
        [FromQuery] string? handoverCondition = null,
        [FromQuery] string? returnCondition = null,
        [FromQuery] int? holdingOverDays = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? departmentScope = null)
    {
        try
        {
            var result = await _service.GetPagedAsync(
                departmentId,
                status,
                page,
                pageSize,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId(),
                keyword,
                employeeId,
                categoryId,
                allocatedFrom,
                allocatedTo,
                handoverCondition,
                returnCondition,
                holdingOverDays,
                sort,
                departmentScope);

            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.AllocationsManage)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _service.GetByIdAsync(
                id,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (AllocationNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Dữ liệu in "Biên bản bàn giao/thu hồi tài sản" theo mẫu (kind=handover|return).
    [HttpGet("{id:int}/document")]
    [HasPermission(Permissions.AllocationsManage)]
    public async Task<IActionResult> GetDocument(int id, [FromQuery] string? kind = null)
    {
        try
        {
            var result = await _service.GetDocumentAsync(
                id, kind, GetCurrentUserRole(), GetCurrentUserDepartmentId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (AllocationNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("overdue")]
    [HasPermission(Permissions.AllocationsManage)]
    public async Task<IActionResult> GetOverdue([FromQuery] int thresholdDays = 180, [FromQuery] string? severity = null)
    {
        try
        {
            var result = await _service.GetOverdueAsync(
                thresholdDays,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId(),
                severity);

            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private string? GetCurrentUserRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetCurrentUserDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId)
            ? departmentId
            : null;
}
