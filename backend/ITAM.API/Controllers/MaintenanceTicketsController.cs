using System.Security.Claims;
using FluentValidation;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.MaintenanceTickets;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/maintenance-tickets")]
[Authorize]
public class MaintenanceTicketsController : ControllerBase
{
    private readonly IMaintenanceTicketService _ticketService;
    private readonly IValidator<CreateMaintenanceTicketRequestDto> _createValidator;
    private readonly IValidator<AssignTechnicianRequestDto> _assignValidator;
    private readonly IValidator<UpdateTicketStatusRequestDto> _statusValidator;

    public MaintenanceTicketsController(
        IMaintenanceTicketService ticketService,
        IValidator<CreateMaintenanceTicketRequestDto> createValidator,
        IValidator<AssignTechnicianRequestDto> assignValidator,
        IValidator<UpdateTicketStatusRequestDto> statusValidator)
    {
        _ticketService = ticketService;
        _createValidator = createValidator;
        _assignValidator = assignValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? departmentId, [FromQuery] int? assetId, [FromQuery] string? status,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _ticketService.GetPagedAsync(
                departmentId, assetId, status, fromDate, toDate, page, pageSize,
                GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _ticketService.GetByIdAsync(
                id, GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (MaintenanceTicketNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost] // UC-11: mọi role đã đăng nhập đều được tạo phiếu.
    public async Task<IActionResult> Create(CreateMaintenanceTicketRequestDto dto)
    {
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _ticketService.CreateAsync(
                dto, GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<object>.Ok(result, "Tạo phiếu bảo trì thành công."));
        }
        catch (AssetNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (AssetNotEligibleForMaintenanceException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketAssignmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id:int}/assign")] // Admin IT/Manager gán tự do; Technician chỉ tự nhận — phân biệt trong Service.
    public async Task<IActionResult> Assign(int id, AssignTechnicianRequestDto dto)
    {
        var validation = await _assignValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _ticketService.AssignTechnicianAsync(
                id, dto, GetCurrentUserRole(), GetCurrentUserDepartmentId(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, "Gán kỹ thuật viên thành công."));
        }
        catch (MaintenanceTicketNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketAssignmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketAlreadyAssignedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketStatusTransitionNotAllowedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin IT,Technician")] // UC-12: Manager bị chặn ngay ở tầng này.
    public async Task<IActionResult> UpdateStatus(int id, UpdateTicketStatusRequestDto dto)
    {
        var validation = await _statusValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        try
        {
            var result = await _ticketService.UpdateStatusAsync(id, dto, GetCurrentUserRole(), GetCurrentUserId());
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật trạng thái phiếu bảo trì thành công."));
        }
        catch (MaintenanceTicketNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketAssignmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (TicketStatusTransitionNotAllowedException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private string? GetCurrentUserRole() => User.FindFirstValue(ClaimTypes.Role);

    private int? GetCurrentUserDepartmentId() =>
        int.TryParse(User.FindFirstValue("DepartmentId"), out var departmentId) ? departmentId : null;

    private int? GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
