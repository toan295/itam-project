using System.Security.Claims;
using ITAM.API.Authorization;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Dashboard;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-18: Admin IT xem toàn hệ thống (lọc được theo phòng ban); Manager chỉ thấy phòng ban mình (service ép phạm vi).
[ApiController]
[Route("api/v1/dashboard")]
[HasPermission(Permissions.DashboardView)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly IRolePermissionCache _permissions;

    public DashboardController(IDashboardService service, IRolePermissionCache permissions)
    {
        _service = service;
        _permissions = permissions;
    }

    // refresh=true bỏ qua cache 60 giây (nút "Làm mới").
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? departmentId = null,
        [FromQuery] int? year = null,
        [FromQuery] bool refresh = false)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        try
        {
            var query = new DashboardQuery(
                role,
                int.TryParse(User.FindFirstValue("DepartmentId"), out var own) ? own : null,
                departmentId,
                year,
                role is null ? new HashSet<string>() : await _permissions.GetAsync(role),
                refresh);
            return Ok(ApiResponse<DashboardSummaryDto>.Ok(await _service.GetSummaryAsync(query)));
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
}
