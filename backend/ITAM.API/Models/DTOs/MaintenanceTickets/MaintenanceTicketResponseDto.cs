namespace ITAM.API.Models.DTOs.MaintenanceTickets;

public class MaintenanceTicketResponseDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public string IssueDescription { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Priority { get; set; } = null!; // Low | Normal | High | Urgent
    public int? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }
    public int? ReportedByUserId { get; set; }
    public string? ReportedByName { get; set; }   // Người tạo phiếu (nhân viên báo lỗi/Admin/Manager).
    public DateTime ReportedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Notes { get; set; }
    public int Version { get; set; }
    public double? ResolutionHours { get; set; } // (ResolvedDate - ReportedDate).TotalHours; null nếu chưa đóng phiếu.
}
