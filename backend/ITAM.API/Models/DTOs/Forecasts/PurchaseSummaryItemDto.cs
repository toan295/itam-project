// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.DTOs.Forecasts;
//
// // "Danh sách mua sắm": gộp thay thế + mua thêm theo loại. Tính khi map DTO, KHÔNG lưu DB.
// public class PurchaseSummaryItemDto
// {
//     public int CategoryId { get; set; }
//     public string CategoryName { get; set; } = null!;
//     public int ReplacementQuantity { get; set; }
//     public int ExpansionQuantity { get; set; }
//     public int TotalQuantity { get; set; }         // = ReplacementQuantity + ExpansionQuantity
//     public decimal? UnitPrice { get; set; }
//     public decimal? TotalCost { get; set; }        // null khi UnitPrice null; ngược lại TotalQuantity * UnitPrice
// }
