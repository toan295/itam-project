namespace ITAM.API.Models.DTOs.SoftwareLicenses;

public class CreateSoftwareLicenseDto
{
    public string SoftwareName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public string? Notes { get; set; }
}
