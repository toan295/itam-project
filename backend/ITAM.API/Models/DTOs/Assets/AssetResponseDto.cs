namespace ITAM.API.Models.DTOs.Assets;

public class AssetResponseDto
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string? SerialNumber { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public bool IsUnderWarranty { get; set; }
}
