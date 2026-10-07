namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleCategoryStatsDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int TotalAssets { get; set; }
    public double? AverageAgeYears { get; set; }
    public double FailureRatio { get; set; }
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }
    public int OverdueNowCount { get; set; }
}
