using ITAM.API.Auditing;
using ITAM.API.Models.DTOs.AuditLogs;
using ITAM.API.Models.DTOs.Common;

namespace ITAM.API.Services.Interfaces;

public interface IAuditLogService
{
    /// <summary>Ghi một sự kiện THÀNH CÔNG (đường tắt của <see cref="RecordEventAsync"/>).</summary>
    Task RecordAsync(
        int userId,
        string action,
        string entityName,
        int entityId,
        object? oldValue = null,
        object? newValue = null);

    /// <summary>Ghi một sự kiện kèm kết quả (Success/Rejected/Failed), lý do, mã HTTP, yêu cầu gốc. Không SaveChanges.</summary>
    Task RecordEventAsync(AuditEvent auditEvent);

    Task<PagedResultDto<AuditLogResponseDto>> GetPagedAsync(
        int page,
        int pageSize,
        int? userId = null,
        string? entityName = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? keyword = null,
        int? entityId = null,
        string? sort = null,
        string? outcome = null);

    Task<AuditLogResponseDto> GetByIdAsync(long id);

    /// <summary>Những người đã có trong nhật ký (cho bộ lọc): không phụ thuộc quyền quản lý người dùng; không lộ email tài khoản đã xoá.</summary>
    Task<List<AuditActorDto>> GetActorsAsync();

    /// <summary>Danh mục đối tượng/hành động/kết quả: toàn bộ chức năng của hệ thống + mọi mã đang có trong dữ liệu, kèm số bản ghi.</summary>
    Task<AuditCatalogDto> GetCatalogAsync();

    /// <summary>Kiểm tra chữ ký từng bản ghi và sự tồn tại của trigger chống sửa/xoá.</summary>
    Task<AuditIntegrityReportDto> VerifyIntegrityAsync();
}
