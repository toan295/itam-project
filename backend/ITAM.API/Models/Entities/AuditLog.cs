namespace ITAM.API.Models.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public int EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
