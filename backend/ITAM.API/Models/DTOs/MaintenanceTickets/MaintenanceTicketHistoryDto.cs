namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class MaintenanceTicketHistoryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Reason { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}
