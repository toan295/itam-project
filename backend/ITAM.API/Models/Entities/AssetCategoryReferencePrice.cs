namespace ITAM.API.Models.Entities;

// Đơn giá tham khảo (VND) theo loại tài sản — dùng để ước tính ngân sách thay thế (UC-17, D7).
// CategoryId vừa là PK vừa là FK → mỗi loại tối đa 1 đơn giá.
public class AssetCategoryReferencePrice
{
    public int CategoryId { get; set; }
    public decimal UnitPrice { get; set; }

    // Xoá mềm: giữ dòng trong DB, chỉ ẩn khỏi mọi truy vấn thường (query filter trong AppDbContext).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public AssetCategory Category { get; set; } = null!;
}
