namespace ITAM.API.Repositories.Models;

// Bộ lọc bổ sung cho danh sách phiếu bảo trì (Service đã validate).
// PendingBefore: chỉ phiếu Pending được báo trước thời điểm này (= "chờ xử lý quá N ngày").
// Sort: urgency (mặc định: việc chờ xử lý lên trước, gấp nhất trước) | newest | oldest.
public class TicketListExtras
{
    public int? TechnicianId { get; set; }
    public bool? Unassigned { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? PendingBefore { get; set; }
    public string? Sort { get; set; }
}
