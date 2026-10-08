namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class UpdateTicketPriorityRequestDto
{
    // "Low" | "Normal" | "High" | "Urgent"
    public string Priority { get; set; } = null!;
}
