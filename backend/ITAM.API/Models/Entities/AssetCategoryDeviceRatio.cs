// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.Entities;
//
// // Định mức số thiết bị trên mỗi nhân sự theo loại tài sản (toàn hệ thống) — UC-17, E3.
// // CategoryId vừa là PK vừa là FK → mỗi loại tối đa 1 định mức.
// public class AssetCategoryDeviceRatio
// {
//     public int CategoryId { get; set; }
//     public decimal DevicesPerPerson { get; set; }   // decimal(6,2), 0,01..20,00
//
//     public int Version { get; set; } = 1;           // optimistic concurrency, tự tăng khi sửa
//     public DateTime? UpdatedAt { get; set; }
//     public int? UpdatedByUserId { get; set; }
//
//     public AssetCategory Category { get; set; } = null!;
// }
