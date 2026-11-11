namespace ITAM.API.Models.Entities;

// Ngưỡng vòng đời RIÊNG của một loại tài sản (tối đa một dòng cho mỗi loại — unique index CategoryId).
// Loại không có dòng này dùng ngưỡng mặc định hệ thống (Lifecycle:DefaultMax* trong cấu hình).
public class AssetCategoryLifecyclePolicy
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }

    // Số phiên bản (optimistic concurrency, tự tăng ở AppDbContext.BumpVersions): hai Admin cùng sửa một loại,
    // hoặc một người lưu trong khi người khác "Về mặc định" -> người đến sau bị 409 thay vì ghi đè lặng lẽ.
    public int Version { get; set; } = 1;
    public DateTime? UpdatedAt { get; set; }   // null = dòng có từ trước khi theo dõi, không rõ thời điểm
    public int? UpdatedByUserId { get; set; }

    public AssetCategory Category { get; set; } = null!;
}
