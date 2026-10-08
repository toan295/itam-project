using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Danh mục nhân viên nhận tài sản. Admin IT toàn quyền; Manager chỉ trong phòng ban của mình.
[ApiController]
[Route("api/v1/employees")]
[Authorize(Roles = "Admin IT,Manager")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;
    private readonly IValidator<UpsertEmployeeRequestDto> _validator;

    public EmployeesController(IEmployeeService service, IValidator<UpsertEmployeeRequestDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId, [FromQuery] string? keyword, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetPagedAsync(
            departmentId, keyword, isActive, page, pageSize, GetRole(), GetDepartmentId());
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public Task<IActionResult> GetById(int id) =>
        Run(async () => Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, GetRole(), GetDepartmentId()))));

    [HttpPost]
    public async Task<IActionResult> Create(UpsertEmployeeRequestDto dto)
    {
        var bad = await ValidateAsync(dto);
        if (bad is not null) return bad;

        return await Run(async () =>
        {
            var result = await _service.CreateAsync(dto, GetRole(), GetDepartmentId());
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.Ok(result, "Đã thêm nhân viên."));
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertEmployeeRequestDto dto)
    {
        var bad = await ValidateAsync(dto);
        if (bad is not null) return bad;

        return await Run(async () =>
            Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, dto, GetRole(), GetDepartmentId()), "Đã cập nhật nhân viên.")));
    }

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id) =>
        Run(async () =>
        {
            await _service.DeleteAsync(id, GetRole(), GetDepartmentId());
            return NoContent();
        });

    private async Task<IActionResult?> ValidateAsync(UpsertEmployeeRequestDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        return validation.IsValid
            ? null
            : BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
    }

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (EmployeeNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (EmployeeConflictException ex)
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

    private string? GetRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId) ? departmentId : null;
}
