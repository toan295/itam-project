using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Models;

// Dòng dữ liệu rút gọn phục vụ thống kê UC-13 — chỉ các cột cần tổng hợp, không kéo cả entity.
public class MaintenanceTicketStatRow
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public DateTime ReportedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
}
