using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.MaintenanceTickets;

namespace ITAM.API.Services.Interfaces;

public interface IMaintenanceTicketService
{
    Task<MaintenanceTicketResponseDto> CreateAsync(
        CreateMaintenanceTicketRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId);

    Task<MaintenanceTicketResponseDto> AssignTechnicianAsync(
        int id, AssignTechnicianRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId);

    Task<MaintenanceTicketResponseDto> UpdateStatusAsync(
        int id, UpdateTicketStatusRequestDto dto, string? currentUserRole, int? currentUserId);

    Task<MaintenanceTicketResponseDto> GetByIdAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId);

    Task<PagedResultDto<MaintenanceTicketResponseDto>> GetPagedAsync(
        int? departmentId, int? assetId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId,
        string? priority = null, bool? closed = null);

    // Admin IT/Manager điều chỉnh mức độ khẩn của phiếu đang chờ xử lý (Manager chỉ phiếu thuộc phòng ban mình).
    Task<MaintenanceTicketResponseDto> UpdatePriorityAsync(
        int id, UpdateTicketPriorityRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId);

    // UC-13: Manager tự động bị giới hạn theo phòng ban mình.
    Task<MaintenanceStatsDto> GetStatsAsync(
        int? departmentId, int? assetId, DateOnly? fromDate, DateOnly? toDate,
        string? currentUserRole, int? currentUserDepartmentId);
}
