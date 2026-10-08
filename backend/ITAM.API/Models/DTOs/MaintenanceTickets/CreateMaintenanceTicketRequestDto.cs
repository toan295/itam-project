namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class CreateMaintenanceTicketRequestDto
{
    public int AssetId { get; set; }
    public string IssueDescription { get; set; } = null!;
    public string? Priority { get; set; } // "Low"|"Normal"|"High"|"Urgent"; bỏ trống = Normal.
    public int? TechnicianId { get; set; } // Tuỳ chọn — UC-11 bước 5.
}
