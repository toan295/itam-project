namespace ITAM.API.Models.DTOs.Lifecycle;

public class UpsertLifecyclePolicyRequestDto
{
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }
}
