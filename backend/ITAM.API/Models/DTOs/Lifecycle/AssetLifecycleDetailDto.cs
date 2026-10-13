namespace ITAM.API.Models.DTOs.Lifecycle;

// M2: chi tiết vòng đời của MỘT tài sản — tương đương ReplacementRecommendationDto nhưng có cả khi chưa vượt ngưỡng
// (IsOverdueNow = false, DueYear có thể null) kèm lịch sử phiếu bảo trì. Chỉ dùng cho giao diện, không thuộc hợp đồng với Dự báo.
public class AssetLifecycleDetailDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public DateOnly? PurchaseDate { get; set; }
    public double? AgeYears { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public bool IsUnderWarranty { get; set; }
    public int TicketCount { get; set; }
    public int FailedTicketCount { get; set; }
    public int AppliedMaxAgeYears { get; set; }
    public int AppliedMaxFailureCount { get; set; }
    public DateOnly? AgeDueDate { get; set; }            // PurchaseDate + ngưỡng tuổi; null khi không có PurchaseDate
    public bool IsOverdueNow { get; set; }
    public bool IsHighPriority { get; set; }
    public int? DueYear { get; set; }                    // null khi không có ngày mua và chưa vượt ngưỡng lỗi
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();   // "AgeExceeded"/"FailureCountExceeded"
    public int PriorityScore { get; set; }               // 0..100, chỉ để tham khảo (L1)
    public string ReasonText { get; set; } = string.Empty;   // rỗng khi chưa vượt ngưỡng
    public IReadOnlyList<AssetTicketSummaryDto> Tickets { get; set; } = Array.Empty<AssetTicketSummaryDto>();
}
