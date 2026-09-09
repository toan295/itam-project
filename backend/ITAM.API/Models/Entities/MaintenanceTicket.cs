using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

public class MaintenanceTicket
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string IssueDescription { get; set; } = null!;
    public TicketStatus Status { get; set; }
    public int? TechnicianId { get; set; }
    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedDate { get; set; }
    public string? Notes { get; set; }

    public Asset Asset { get; set; } = null!;
    public User? Technician { get; set; }
}
