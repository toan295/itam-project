namespace ITAM.API.Models.DTOs.Lifecycle;

// Một dòng phiếu bảo trì trong trang chi tiết vòng đời của tài sản (M2).
public class AssetTicketSummaryDto
{
    public int Id { get; set; }
    public DateTime ReportedDate { get; set; }
    public string Status { get; set; } = null!;          // "Pending" | "Resolved" | "Failed"
    public DateTime? ResolvedDate { get; set; }
    public string Description { get; set; } = string.Empty;   // cắt tối đa 200 ký tự
}
