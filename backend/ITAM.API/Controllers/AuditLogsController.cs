using ITAM.API.Authorization;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.AuditLogs;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[HasPermission(Permissions.AuditLogsView)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AuditLogsController(IAuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<AuditLogResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? userId = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int? entityId = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? outcome = null)
    {
        try
        {
            var result = await _service.GetPagedAsync(
                page,
                pageSize,
                userId,
                entityName,
                action,
                fromDate,
                toDate,
                keyword,
                entityId,
                sort,
                outcome);

            return Ok(ApiResponse<PagedResultDto<AuditLogResponseDto>>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Người đã có trong nhật ký (cho ô lọc "Người dùng"): email chỉ hiển thị cho người giữ quyền audit-logs.view (Admin IT),
    // và không bao giờ hiển thị email của tài khoản đã xoá.
    [HttpGet("actors")]
    public async Task<IActionResult> GetActors() =>
        Ok(ApiResponse<List<AuditActorDto>>.Ok(await _service.GetActorsAsync()));

    // Danh mục đối tượng/hành động/kết quả: toàn bộ chức năng hệ thống + mọi mã đang có trong dữ liệu, kèm số bản ghi.
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog() =>
        Ok(ApiResponse<AuditCatalogDto>.Ok(await _service.GetCatalogAsync()));

    // Kiểm tra chữ ký từng bản ghi và trigger chống sửa/xoá.
    [HttpGet("integrity")]
    public async Task<IActionResult> VerifyIntegrity() =>
        Ok(ApiResponse<AuditIntegrityReportDto>.Ok(await _service.VerifyIntegrityAsync()));

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<AuditLogResponseDto>.Ok(result));
        }
        catch (AuditLogNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
