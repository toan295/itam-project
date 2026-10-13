namespace ITAM.API.Models.DTOs.Lifecycle;

// DTO mở rộng cho giao diện Tuần 8. Hợp đồng AssetReplacementCandidateDto được giữ nguyên.
public class ReplacementRecommendationDto : AssetReplacementCandidateDto
{
    public int PriorityScore { get; set; }
    public string ReasonText { get; set; } = string.Empty;
    public DateOnly? WarrantyExpiry { get; set; }
    public bool IsUnderWarranty { get; set; }
    public int AppliedMaxAgeYears { get; set; }
    public int AppliedMaxFailureCount { get; set; }
}
