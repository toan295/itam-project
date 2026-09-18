using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin IT")]
[Route("api/v1/software-licenses")]
public class SoftwareLicensesController : ControllerBase
{
    private readonly ISoftwareLicenseService _service;
    private readonly IValidator<CreateSoftwareLicenseDto> _createValidator;
    private readonly IValidator<UpdateSoftwareLicenseDto> _updateValidator;
    private readonly IValidator<AssignSoftwareLicenseDto> _assignValidator;

    public SoftwareLicensesController(
        ISoftwareLicenseService service,
        IValidator<CreateSoftwareLicenseDto> createValidator,
        IValidator<UpdateSoftwareLicenseDto> updateValidator,
        IValidator<AssignSoftwareLicenseDto> assignValidator)
    {
        _service = service;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _assignValidator = assignValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<SoftwareLicenseResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, search);
        return Ok(ApiResponse<PagedResultDto<SoftwareLicenseResponseDto>>.Ok(result));
    }

    [HttpGet("expiring-soon")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SoftwareLicenseResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpiringSoon([FromQuery] int days = 30)
    {
        if (days is < 1 or > 365)
        {
            return BadRequest(ApiResponse<object>.Fail("Số ngày cảnh báo phải nằm trong khoảng từ 1 đến 365."));
        }

        var result = await _service.GetExpiringSoonAsync(days);
        return Ok(ApiResponse<IReadOnlyList<SoftwareLicenseResponseDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SoftwareLicenseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<SoftwareLicenseResponseDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SoftwareLicenseResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateSoftwareLicenseDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                ApiResponse<SoftwareLicenseResponseDto>.Ok(result, "Tạo license thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SoftwareLicenseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSoftwareLicenseDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

        try
        {
            var result = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<SoftwareLicenseResponseDto>.Ok(result, "Cập nhật license thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{id:int}/assign")]
    [ProducesResponseType(typeof(ApiResponse<SoftwareLicenseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignSoftwareLicenseDto dto)
    {
        var validation = await _assignValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return ValidationError(validation.Errors.Select(error => error.ErrorMessage));
        }

        try
        {
            var result = await _service.AssignAsync(id, dto.AssetId);
            return Ok(ApiResponse<SoftwareLicenseResponseDto>.Ok(result, "Gán license cho tài sản thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:int}/assign/{assetId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(int id, int assetId)
    {
        try
        {
            await _service.UnassignAsync(id, assetId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private BadRequestObjectResult ValidationError(IEnumerable<string> errors)
    {
        return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", errors.ToList()));
    }
}
