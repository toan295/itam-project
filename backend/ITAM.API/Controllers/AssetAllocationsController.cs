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
    [Authorize(Roles = "Admin IT,Manager")]
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
                GetCurrentUserDepartmentId());

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
    [Authorize(Roles = "Admin IT,Manager")]
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
                GetCurrentUserDepartmentId());

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
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _service.GetPagedAsync(
                departmentId,
                status,
                page,
                pageSize,
                GetCurrentUserRole(),
                GetCurrentUserDepartmentId());

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

    [HttpGet("overdue")]
    [Authorize(Roles = "Admin IT,Manager")]
    public async Task<IActionResult> GetOverdue([FromQuery] int thresholdDays = 180)
    {
        var result = await _service.GetOverdueAsync(
            thresholdDays,
            GetCurrentUserRole(),
            GetCurrentUserDepartmentId());

        return Ok(ApiResponse<object>.Ok(result));
    }

    private string? GetCurrentUserRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetCurrentUserDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId)
            ? departmentId
            : null;
}
