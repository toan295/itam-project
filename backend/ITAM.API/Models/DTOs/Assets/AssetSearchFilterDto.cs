namespace ITAM.API.Models.DTOs.Assets;

public class AssetSearchFilterDto
{
    public string? Keyword { get; set; }       // Tìm theo Name hoặc AssetCode (LIKE).
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public string? Status { get; set; }
    public int? PurchaseYear { get; set; }
    public string? WarrantyStatus { get; set; } // "Valid" | "Expired" | null = không lọc.
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
