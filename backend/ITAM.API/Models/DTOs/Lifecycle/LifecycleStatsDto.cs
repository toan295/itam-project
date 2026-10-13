namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleStatsDto
{
    public int TotalAssets { get; set; }                       // không tính Disposed
    public int AssetsWithoutPurchaseDate { get; set; }
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
