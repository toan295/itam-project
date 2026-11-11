using System.Security.Claims;
using ITAM.API.Authorization;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Permissions;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Ma trận phân quyền: vai trò × chức năng. Chỉ người giữ quyền hệ thống "permissions.manage" (Admin IT).
[ApiController]
[Route("api/v1/permissions")]
[HasPermission(Permissions.PermissionsManage)]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _service;

    public PermissionsController(IPermissionService service)
    {
        _service = service;
    }

    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix() =>
        Ok(ApiResponse<PermissionMatrixDto>.Ok(await _service.GetMatrixAsync()));

    [HttpPut("roles/{roleId:int}")]
    public Task<IActionResult> UpdateRole(int roleId, UpdateRolePermissionsRequestDto dto) =>
        RunAsync(() => _service.UpdateRoleAsync(roleId, dto, CurrentUserId()), "Đã lưu phân quyền.");

    [HttpPost("roles/{roleId:int}/reset")]
    public Task<IActionResult> ResetRole(
        int roleId, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ResetRolePermissionsRequestDto? dto = null) =>
        RunAsync(() => _service.ResetRoleAsync(roleId, dto, CurrentUserId()), "Đã khôi phục quyền mặc định.");

    // Lưu cả ma trận một lần (nguyên tử); trả lại ma trận mới do server xác nhận để giao diện hiển thị đúng dữ liệu thật.
    [HttpPut("matrix")]
    public Task<IActionResult> UpdateMatrix(UpdatePermissionMatrixRequestDto dto) =>
        RunAsync(() => _service.UpdateMatrixAsync(dto, CurrentUserId()), "Đã lưu phân quyền.");

    private async Task<IActionResult> RunAsync<T>(Func<Task<T>> action, string message)
    {
        try
        {
            return Ok(ApiResponse<T>.Ok(await action(), message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
