namespace ITAM.API.Models.DTOs.AuditLogs;

public class AuditLogResponseDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}
