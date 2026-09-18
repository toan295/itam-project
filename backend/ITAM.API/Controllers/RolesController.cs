using ITAM.API.Models.DTOs;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Danh mục Role cố định (Admin IT, Manager, Technician) — chỉ đọc, phục vụ form quản lý người
// dùng (UC-03). Không có CRUD vì việc thêm/sửa Role ảnh hưởng tới toàn bộ policy phân quyền,
// là quyết định kiến trúc lớn ngoài phạm vi hiện tại.
[ApiController]
[Route("api/v1/roles")]
[Authorize(Roles = "Admin IT")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _roleService.GetAllAsync();
        return Ok(ApiResponse<object>.Ok(result));
    }
}
