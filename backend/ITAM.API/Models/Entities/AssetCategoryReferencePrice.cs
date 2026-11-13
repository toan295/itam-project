// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.Entities;
//
// // Đơn giá tham khảo (VND) theo loại tài sản — dùng để ước tính ngân sách thay thế (UC-17, D7).
// // CategoryId vừa là PK vừa là FK → mỗi loại tối đa 1 đơn giá.
// public class AssetCategoryReferencePrice
// {
//     public int CategoryId { get; set; }
//     public decimal UnitPrice { get; set; }
//
//     // Optimistic concurrency (tự tăng ở AppDbContext.BumpVersions) + ai/lúc nào sửa gần nhất.
//     public int Version { get; set; } = 1;
//     public DateTime? UpdatedAt { get; set; }
//     public int? UpdatedByUserId { get; set; }
//
//     // Xoá mềm: giữ dòng trong DB, chỉ ẩn khỏi mọi truy vấn thường (query filter trong AppDbContext).
//     public bool IsDeleted { get; set; }
//     public DateTime? DeletedAt { get; set; }
//
//     public AssetCategory Category { get; set; } = null!;
// }
