namespace ITAM.API.Models.DTOs.Assets;

public class UpdateAssetRequestDto
{
    // UC-06 bước 3: cho phép đổi AssetCode, Service sẽ kiểm tra trùng mã nếu giá trị này thay đổi.
    public string AssetCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int CategoryId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public int DepartmentId { get; set; }

    // "InUse" | "Maintenance" | "Broken" | "Disposed" (ITAM.API.Models.Enums.AssetStatus).
    public string Status { get; set; } = default!;

    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
}
