namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecyclePolicyDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }
    public bool IsOverridden { get; set; }
}
