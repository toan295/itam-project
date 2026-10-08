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
    // Chỉ thêm vào change tracker — KHÔNG lưu. Nơi gọi (UserService) tự SaveChangesAsync một lần
    // cùng với thay đổi nghiệp vụ để dòng log và dữ liệu được ghi atomic (Unit-of-Work).
    void Add(AuditLog auditLog);
}
