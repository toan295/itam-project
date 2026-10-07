namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecycleEvaluation
{
    public int? DueYear { get; set; }
    public bool IsOverdueNow { get; set; }
    public int Priority { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
