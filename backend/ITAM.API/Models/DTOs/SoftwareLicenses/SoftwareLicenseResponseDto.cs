namespace ITAM.API.Models.DTOs.SoftwareLicenses;

public class SoftwareLicenseResponseDto
{
    public int Id { get; set; }
    public string SoftwareName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public int CurrentUsage { get; set; }
    public decimal UsagePercentage { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public bool IsNearUsageLimit { get; set; }
    public string? Notes { get; set; }
}
