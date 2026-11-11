using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

public class MaintenanceTicket
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string IssueDescription { get; set; } = null!;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public int? TechnicianId { get; set; }
    // Người tạo phiếu (nhân viên báo lỗi, Admin IT hoặc Manager). Null = phiếu cũ trước khi có cột này.
    public int? ReportedByUserId { get; set; }
    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedDate { get; set; }
    public string? Notes { get; set; }

    // Số phiên bản bản ghi — xem Asset.Version.
    public int Version { get; set; } = 1;

    public Asset Asset { get; set; } = null!;
    public User? Technician { get; set; }
    public User? ReportedBy { get; set; }
    public ICollection<MaintenanceTicketHistory> History { get; set; } = new List<MaintenanceTicketHistory>();
}
