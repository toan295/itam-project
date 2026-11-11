namespace ITAM.API.Models.DTOs.Assets;

public class AssetSearchFilterDto
{
    public string? Keyword { get; set; }       // Tìm theo Name, AssetCode hoặc SerialNumber (LIKE).
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public string? Status { get; set; }
    public int? PurchaseYear { get; set; }
    public string? WarrantyStatus { get; set; } // "Valid" | "Expired" | null = không lọc.
    public string? AllocationState { get; set; }   // "allocated" (đang được cấp cho nhân viên) | "free" (chưa cấp).
    public int? WarrantyExpiringInDays { get; set; } // 1–365: bảo hành còn hiệu lực nhưng hết trong N ngày tới.
    public DateOnly? PurchasedFrom { get; set; }
    public DateOnly? PurchasedTo { get; set; }
    public string? Sort { get; set; }              // code (mặc định) | name | purchaseDate | warrantyExpiry | newest
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
