namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleCategoryStatsDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int AssetCount { get; set; }
    public double? AverageAgeYears { get; set; }
    public int AssetsWithAtLeastOneTicket { get; set; }
    public double FailureRatio { get; set; }
    public int MaxAgeYears { get; set; }          // ngưỡng hiệu lực (đã gộp mặc định)
    public int MaxFailureCount { get; set; }
    public int OverdueNowCount { get; set; }
}
