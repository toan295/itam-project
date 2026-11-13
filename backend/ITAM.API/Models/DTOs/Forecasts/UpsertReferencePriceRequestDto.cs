// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.DTOs.Forecasts;
//
// public class UpsertReferencePriceRequestDto
// {
//     public decimal UnitPrice { get; set; }
//
//     // null = "loại này chưa có đơn giá, tôi tạo mới"; có giá trị = "tôi đang sửa đúng bản này". Lệch trạng thái hiện tại -> 409.
//     public int? Version { get; set; }
// }
