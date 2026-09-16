namespace ITAM.API.Models.DTOs.Assets;

public class CreateAssetRequestDto
{
    public string AssetCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int CategoryId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public int DepartmentId { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
}
