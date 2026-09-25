using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-04: Quản lý danh mục phòng ban — CRUD đầy đủ, chỉ Admin IT được thêm/sửa/xoá (mọi vai trò đọc được để
// đổ dropdown). Mục 8.2 chỉ liệt kê GET/POST nhưng UC-04 yêu cầu "thêm / sửa / xoá" + chặn xoá khi đang được tham chiếu.
[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IValidator<CreateDepartmentRequestDto> _createValidator;
    private readonly IValidator<UpdateDepartmentRequestDto> _updateValidator;

    public DepartmentsController(
        IDepartmentService departmentService,
        IValidator<CreateDepartmentRequestDto> createValidator,
        IValidator<UpdateDepartmentRequestDto> updateValidator)
    {
        _departmentService = departmentService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _departmentService.GetAllAsync();
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _departmentService.GetByIdAsync(id);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (DepartmentNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Create(CreateDepartmentRequestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _departmentService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<object>.Ok(result, "Thêm phòng ban thành công."));
        }
        catch (DepartmentNameAlreadyExistsException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin IT")]
    public async Task<IActionResult> Update(int id, UpdateDepartmentRequestDto dto)
    {
        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _departmentService.UpdateAsync(id, dto);
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật phòng ban thành công."));
        }
        catch (DepartmentNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentNameAlreadyExistsException ex)
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
            await _departmentService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Ok(new { }, "Đã xoá phòng ban."));
        }
        catch (DepartmentNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (DepartmentInUseException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
