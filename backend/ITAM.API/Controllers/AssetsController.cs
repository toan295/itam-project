using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly IValidator<CreateAssetRequestDto> _createValidator;
    private readonly IValidator<UpdateAssetRequestDto> _updateValidator;

    public AssetsController(
        IAssetService assetService,
        IValidator<CreateAssetRequestDto> createValidator,
        IValidator<UpdateAssetRequestDto> updateValidator)
    {
        _assetService = assetService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _assetService.GetPagedAsync(
                departmentId, status, page, pageSize,
                GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
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
            var result = await _assetService.GetByIdAsync(
                id, GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("search")] // UC-08: tìm kiếm & lọc tài sản nâng cao.
    public async Task<IActionResult> Search([FromQuery] AssetSearchFilterDto filter)
    {
        try
        {
            var result = await _assetService.SearchAsync(
                filter, GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin IT,Manager")] // UC-05: chỉ Admin IT và Manager được thêm tài sản.
    public async Task<IActionResult> Create(CreateAssetRequestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _assetService.CreateAsync(dto, GetCurrentUserRole(), GetCurrentUserDepartmentId());
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<object>.Ok(result, "Thêm tài sản thành công."));
        }
        catch (AssetCodeAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin IT,Manager")] // UC-06: chỉ Admin IT và Manager được sửa tài sản.
    public async Task<IActionResult> Update(int id, UpdateAssetRequestDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _assetService.UpdateAsync(id, dto, GetCurrentUserRole(), GetCurrentUserDepartmentId());
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật tài sản thành công."));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetDisposalNotAllowedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetReactivationNotAllowedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetCodeAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin IT")] // UC-07: chỉ Admin IT được ngừng sử dụng tài sản.
    public async Task<IActionResult> Dispose(int id)
    {
        try
        {
            var result = await _assetService.DisposeAsync(id);
            return Ok(ApiResponse<object>.Ok(result, "Đã chuyển tài sản sang trạng thái Disposed."));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetHasOpenAllocationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private string? GetCurrentUserRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetCurrentUserDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId) ? departmentId : null;

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
