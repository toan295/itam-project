using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.AssetCategories;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-04: Quản lý danh mục loại tài sản — CRUD dùng chung cho toàn hệ thống (Mục 8.2: /api/v1/asset-categories).
[ApiController]
[Route("api/v1/asset-categories")]
[Authorize]
public class AssetCategoriesController : ControllerBase
{
    private readonly IAssetCategoryService _categoryService;
    private readonly IValidator<CreateAssetCategoryRequestDto> _createValidator;
    private readonly IValidator<UpdateAssetCategoryRequestDto> _updateValidator;

    public AssetCategoriesController(
        IAssetCategoryService categoryService,
        IValidator<CreateAssetCategoryRequestDto> createValidator,
        IValidator<UpdateAssetCategoryRequestDto> updateValidator)
    {
        _categoryService = categoryService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _categoryService.GetAllAsync();
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _categoryService.GetByIdAsync(id);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (AssetCategoryNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Create(CreateAssetCategoryRequestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _categoryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<object>.Ok(result, "Thêm loại tài sản thành công."));
        }
        catch (AssetCategoryNameAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Update(int id, UpdateAssetCategoryRequestDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _categoryService.UpdateAsync(id, dto);
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật loại tài sản thành công."));
        }
        catch (AssetCategoryNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetCategoryNameAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _categoryService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(new { }, "Đã xoá loại tài sản."));
        }
        catch (AssetCategoryNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetCategoryInUseException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
