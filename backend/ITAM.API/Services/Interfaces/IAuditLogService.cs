using ITAM.API.Models.DTOs.AuditLogs;
using ITAM.API.Models.DTOs.Common;

namespace ITAM.API.Services.Interfaces;

public interface IAuditLogService
{
    Task RecordAsync(
        int userId,
        string action,
        string entityName,
        int entityId,
        object? oldValue = null,
        object? newValue = null);

    Task<PagedResultDto<AuditLogResponseDto>> GetPagedAsync(
        int page,
        int pageSize,
        int? userId = null,
        string? entityName = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    Task<AuditLogResponseDto> GetByIdAsync(long id);
}
