namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleDepartmentStatsDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int TotalAssets { get; set; }
    public double? AverageAgeYears { get; set; }
    public double FailureRatio { get; set; }
    public double AverageTicketsPerAsset { get; set; }
    public int OverdueNowCount { get; set; }
}
