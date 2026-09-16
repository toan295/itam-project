namespace ITAM.API.Repositories.Models;

public class SoftwareLicenseSnapshot
{
    public int Id { get; set; }
    public string SoftwareName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public string? Notes { get; set; }
    public int CurrentUsage { get; set; }
}
