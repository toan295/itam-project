namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class UpdateTicketStatusRequestDto
{
    public string Status { get; set; } = null!; // Chỉ "Resolved" hoặc "Failed" — không chấp nhận chuỗi số.
    public string? Notes { get; set; }
}
