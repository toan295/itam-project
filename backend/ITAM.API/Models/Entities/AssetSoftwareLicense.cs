namespace ITAM.API.Models.Entities;

public class AssetSoftwareLicense
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int LicenseId { get; set; }
    public DateOnly AssignedDate { get; set; }

    public Asset Asset { get; set; } = null!;
    public SoftwareLicense License { get; set; } = null!;
}
