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
[Authorize(Roles = "Admin IT")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AuditLogsController(IAuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResultDto<AuditLogResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? userId = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await _service.GetPagedAsync(
            page,
            pageSize,
            userId,
            entityName,
            action,
            fromDate,
            toDate);

        return Ok(ApiResponse<PagedResultDto<AuditLogResponseDto>>.Ok(result));
    }

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
