namespace ITAM.API.Models.DTOs.MaintenanceTickets;

// UC-13: thống kê phiếu bảo trì.
public class MaintenanceStatsDto
{
    // Luôn đủ 3 khoá Pending/Resolved/Failed (giá trị 0 nếu không có phiếu) để frontend không phải kiểm tra thiếu khoá.
    public Dictionary<string, int> CountByStatus { get; set; } = new();

    // Trung bình (ResolvedDate - ReportedDate) theo giờ, chỉ tính phiếu đã Resolved/Failed; null nếu chưa có phiếu nào đóng.
    public double? AverageResolutionHours { get; set; }

    public List<AssetMaintenanceYearlyCountDto> ByAssetPerYear { get; set; } = new();
}

public class AssetMaintenanceYearlyCountDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public int Year { get; set; }
    public int TicketCount { get; set; }
}
