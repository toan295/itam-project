using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Models;

// Điều kiện tìm kiếm tài sản đã được Service parse/validate (enum, phạm vi phòng ban).
public class AssetQueryCriteria
{
    public string? Keyword { get; set; }
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public AssetStatus? Status { get; set; }
    public int? PurchaseYear { get; set; }
    public bool? IsUnderWarranty { get; set; }
    public int? OrAssignedTechnicianId { get; set; }
    public bool? IsAllocated { get; set; }
    public int? WarrantyExpiringInDays { get; set; }
    public DateOnly? PurchasedFrom { get; set; }
    public DateOnly? PurchasedTo { get; set; }
    public string? Sort { get; set; }
}
