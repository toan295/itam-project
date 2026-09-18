using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-04: Quản lý danh mục phòng ban (Mục 8.2: GET/POST /api/v1/departments — không có PUT/DELETE).
[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    private readonly IValidator<CreateDepartmentRequestDto> _createValidator;

    public DepartmentsController(
        IDepartmentService departmentService,
        IValidator<CreateDepartmentRequestDto> createValidator)
    {
        _departmentService = departmentService;
        _createValidator = createValidator;
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
}
