namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class UpdateTicketStatusRequestDto
{
    public string Status { get; set; } = null!; // Chỉ "Resolved" hoặc "Failed" — không chấp nhận chuỗi số.
    // Kết quả xử lý (bắt buộc): việc đã làm khi Resolved, hoặc lý do không xử lý được khi Failed.
    public string? Notes { get; set; }
    public int? Version { get; set; } // Số phiên bản client đang giữ; lệch -> 409.
}
