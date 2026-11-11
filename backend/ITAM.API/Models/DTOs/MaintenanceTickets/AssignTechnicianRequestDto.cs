namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class AssignTechnicianRequestDto
{
    public int TechnicianId { get; set; }

    // Bắt buộc khi ĐỔI người đã được phân công (để truy vết vì sao điều phối lại).
    public string? Reason { get; set; }
    public int? Version { get; set; } // Số phiên bản client đang giữ; lệch -> 409.
}
