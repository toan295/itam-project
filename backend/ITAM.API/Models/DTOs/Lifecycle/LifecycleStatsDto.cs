namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleStatsDto
{
    // Phạm vi của MỌI con số dưới đây: tài sản KHÁC Disposed, thoả bộ lọc phòng ban/loại đã gửi; tuổi tính đến AsOfDate (ngày VN).
    public DateOnly AsOfDate { get; set; }
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public int TotalAssets { get; set; }                       // không tính Disposed
    public int AssetsWithoutPurchaseDate { get; set; }         // ngày mua null
    public int AssetsWithFuturePurchaseDate { get; set; }      // ngày mua ở tương lai = dữ liệu sai, không tính tuổi
    public int TotalTickets { get; set; }                      // tổng phiếu bảo trì (mọi trạng thái) của các tài sản trong phạm vi
    public double? AverageAgeYears { get; set; }               // chỉ tính tài sản CÓ PurchaseDate; không có → null
    public double? MedianAgeYears { get; set; }
    public int AssetsWithAtLeastOneTicket { get; set; }
    public double FailureRatio { get; set; }                   // AssetsWithAtLeastOneTicket / TotalAssets (0 khi TotalAssets = 0)
    public double AverageTicketsPerAsset { get; set; }         // 0 khi TotalAssets = 0
    public int OverdueNowCount { get; set; }
    public IReadOnlyList<LifecycleCategoryStatsDto> ByCategory { get; set; } = Array.Empty<LifecycleCategoryStatsDto>();
    public IReadOnlyList<LifecycleAgeBucketDto> AgeBuckets { get; set; } = Array.Empty<LifecycleAgeBucketDto>();
    public IReadOnlyList<LifecycleDepartmentStatsDto> ByDepartment { get; set; } = Array.Empty<LifecycleDepartmentStatsDto>();
    public IReadOnlyList<TopFailingAssetDto> TopFailingAssets { get; set; } = Array.Empty<TopFailingAssetDto>();
    public IReadOnlyList<TicketTrendPointDto> TicketTrend { get; set; } = Array.Empty<TicketTrendPointDto>();
}
