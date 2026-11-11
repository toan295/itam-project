using System.Security.Claims;
using ITAM.API.Models.DTOs;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Cổng nhân viên: chỉ tài khoản vai trò Employee, và chỉ thấy dữ liệu của CHÍNH MÌNH (suy ra từ token, không nhận id từ client).
[ApiController]
[Route("api/v1/my")]
[Authorize(Roles = "Employee")]
public class MyController : ControllerBase
{
    private readonly IEmployeePortalService _portal;

    public MyController(IEmployeePortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("assets")]
    public async Task<IActionResult> GetMyAssets() =>
        Ok(ApiResponse<object>.Ok(await _portal.GetMyAssetsAsync(GetCurrentUserId())));

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(ApiResponse<object>.Ok(await _portal.GetMyTicketsAsync(GetCurrentUserId(), page, pageSize)));

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
