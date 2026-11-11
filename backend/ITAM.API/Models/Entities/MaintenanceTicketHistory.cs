namespace ITAM.API.Models.Entities;

// Một sự kiện trong vòng đời phiếu bảo trì (tạo, phân công, đổi người, đổi mức độ, đóng phiếu...). Ghi cùng lần lưu
// với thay đổi của phiếu nên không bao giờ lệch nhau; giá trị trước/sau lưu dạng chữ để lịch sử đọc được độc lập.
public class MaintenanceTicketHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string Action { get; set; } = null!; // Created | Assigned | Reassigned | PriorityChanged | Resolved | Failed | AutoCreated | AutoClosed
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Reason { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MaintenanceTicket Ticket { get; set; } = null!;
}
