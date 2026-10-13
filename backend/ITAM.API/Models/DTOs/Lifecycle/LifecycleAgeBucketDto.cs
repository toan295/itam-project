namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleAgeBucketDto
{
    public string Label { get; set; } = null!;
    public int Count { get; set; }
    public int AssetsWithAtLeastOneTicket { get; set; }
    public double FailureRatio { get; set; }
    public double AverageTicketsPerAsset { get; set; }
}
