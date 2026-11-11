namespace ITAM.API.Auditing;

/// <summary>Một sự kiện cần ghi nhật ký. Mặc định là thao tác thành công; truyền Outcome khác cho thao tác bị từ chối / thất bại.</summary>
public sealed record AuditEvent(
    int UserId,
    string Action,
    string EntityName,
    int EntityId,
    string Outcome = Models.Entities.AuditOutcome.Success,
    object? OldValue = null,
    object? NewValue = null,
    int? StatusCode = null,
    string? Message = null,
    object? Detail = null);
