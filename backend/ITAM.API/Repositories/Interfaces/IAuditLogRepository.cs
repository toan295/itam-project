using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task<(List<AuditLog> Items, int TotalItems)> GetPagedAsync(
        int page,
        int pageSize,
        int? userId,
        string? entityName,
        string? action,
        DateTime? fromDate,
        DateTime? toDate);

    Task<AuditLog?> GetByIdWithDetailsAsync(long id);
    Task AddAsync(AuditLog auditLog);
    Task<int> SaveChangesAsync();
}
