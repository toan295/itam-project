namespace ITAM.API.Models.DTOs.Lifecycle;

public class AssetReplacementCandidateDto
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
    public int TicketCount { get; set; }
    public int FailedTicketCount { get; set; }
    public double FailureRatePerYear { get; set; }
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
    public bool IsOverdueNow { get; set; }
    public int Priority { get; set; }
    public int? DueYear { get; set; }
}
