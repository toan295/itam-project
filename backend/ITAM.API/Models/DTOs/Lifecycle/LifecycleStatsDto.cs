namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleStatsDto
{
    public int TotalAssets { get; set; }
    public double? AverageAgeYears { get; set; }
    public double FailureRatio { get; set; }
    public double AverageTicketsPerAsset { get; set; }
    public int OverdueNowCount { get; set; }
    public int UnknownPurchaseDateCount { get; set; }
    public IReadOnlyList<LifecycleCategoryStatsDto> ByCategory { get; set; } = Array.Empty<LifecycleCategoryStatsDto>();
    public IReadOnlyList<LifecycleAgeBucketDto> AgeBuckets { get; set; } = Array.Empty<LifecycleAgeBucketDto>();
}
