namespace ITAM.API.Models.DTOs.Lifecycle;

public class UpcomingReplacementDto
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
    public DateOnly? AgeDueDate { get; set; }
    public int? DaysUntilAgeDue { get; set; }          // chỉ có giá trị khi AgeDueSoon (ngày đủ tuổi nằm trong cửa sổ); ngược lại null
    public bool AgeDueSoon { get; set; }
    public bool NearFailureThreshold { get; set; }
    public int AppliedMaxAgeYears { get; set; }
    public int AppliedMaxFailureCount { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();   // chỉ "AgeDueSoon"/"NearFailureThreshold" — không dịch
    public string ReasonText { get; set; } = string.Empty;
}
