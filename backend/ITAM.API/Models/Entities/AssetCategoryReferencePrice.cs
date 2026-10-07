namespace ITAM.API.Models.Entities;

// Đơn giá tham khảo (VND) theo loại tài sản — dùng để ước tính ngân sách thay thế (UC-17, D7).
// CategoryId vừa là PK vừa là FK → mỗi loại tối đa 1 đơn giá.
public class AssetCategoryReferencePrice
{
    public int CategoryId { get; set; }
    public decimal UnitPrice { get; set; }

    public AssetCategory Category { get; set; } = null!;
}
