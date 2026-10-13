namespace ITAM.API.Models.DTOs.Lifecycle;

// Kết quả nội bộ của phép đánh giá "sắp tới hạn".
public class LifecycleUpcomingEvaluation
{
    public bool AgeDueSoon { get; set; }
    public bool NearFailureThreshold { get; set; }
    public DateOnly? AgeDueDate { get; set; }
    public int? DaysUntilAgeDue { get; set; }
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
    public string ReasonText { get; set; } = string.Empty;
}
