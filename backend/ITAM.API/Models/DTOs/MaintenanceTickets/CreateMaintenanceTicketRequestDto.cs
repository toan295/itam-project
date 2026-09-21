namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class CreateMaintenanceTicketRequestDto
{
    public int AssetId { get; set; }
    public string IssueDescription { get; set; } = null!;
    public int? TechnicianId { get; set; } // Tuỳ chọn — UC-11 bước 5.
}
