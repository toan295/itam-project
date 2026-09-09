namespace ITAM.API.Models.Entities;

public class SoftwareLicense
{
    public int Id { get; set; }
    public string SoftwareName { get; set; } = null!;
    public string LicenseKey { get; set; } = null!;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public string? Notes { get; set; }

    public ICollection<AssetSoftwareLicense> AssetSoftwareLicenses { get; set; } = new List<AssetSoftwareLicense>();
}
