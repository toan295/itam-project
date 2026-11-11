using ITAM.API.Repositories.Models;
using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public record AuditActorSnapshot(string Name, string Role);

public record AuditActorRow(int Id, string Name, string Email, bool IsDeleted, string? Role, int EventCount);

public interface IAuditLogRepository
{
    /// <param name="fromUtc">Cận dưới (bao gồm), đã đổi sang UTC.</param>
    /// <param name="toUtcExclusive">Cận trên (KHÔNG bao gồm), đã đổi sang UTC.</param>
    Task<(List<AuditLog> Items, int TotalItems)> GetPagedAsync(
        int page,
        int pageSize,
        int? userId,
        string? entityName,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtcExclusive,
        AuditLogListExtras extras);

    Task<AuditLog?> GetByIdWithDetailsAsync(long id);

    // Chỉ thêm vào change tracker — KHÔNG lưu. Nơi gọi (UserService) tự SaveChangesAsync một lần
    // cùng với thay đổi nghiệp vụ để dòng log và dữ liệu được ghi atomic (Unit-of-Work).
    void Add(AuditLog auditLog);

    Task<AuditActorSnapshot?> GetActorSnapshotAsync(int userId);
    Task<List<AuditActorRow>> GetActorsAsync();
    Task<Dictionary<string, int>> CountByAsync(string column);
    Task<List<AuditLog>> GetBatchAsync(long afterId, int take);
    Task<bool> ImmutabilityTriggersPresentAsync();
}
