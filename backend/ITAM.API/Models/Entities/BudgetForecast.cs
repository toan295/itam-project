// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.Entities;
//
// public class BudgetForecast
// {
//     public int Id { get; set; }
//     public int Year { get; set; }
//     public int DepartmentId { get; set; }
//     public int EstimatedReplacementCount { get; set; }
//     /// <summary>Ngân sách của phần THAY THẾ mà thôi (E8). Tổng = EstimatedBudget + EstimatedExpansionBudget.</summary>
//     public decimal EstimatedBudget { get; set; }
//     public string? Notes { get; set; }
//     public string? BreakdownJson { get; set; }   // chi tiết theo loại tài sản (JSON camelCase)
//     public DateTime? GeneratedAt { get; set; }   // lần tạo/cập nhật dự báo gần nhất (UTC)
//
//     // Tái lập & phát hiện dữ liệu đầu vào đã đổi: InputsJson là ảnh chụp đầu vào của lần tạo (nhân sự, định mức, đơn giá, tồn kho,
//     // danh sách tài sản đến hạn); InputsHash = SHA-256 của nó. Chạy lại với đầu vào y hệt thì không ghi gì (Revision giữ nguyên).
//     public int Revision { get; set; } = 1;       // tăng mỗi khi lần chạy lại thực sự đổi kết quả
//     public string? InputsHash { get; set; }      // null = dòng cũ trước khi lưu ảnh chụp đầu vào
//     public string? InputsJson { get; set; }
//
//     // Phần MUA THÊM theo kế hoạch mở rộng phòng ban (Tuần 8, E8). Dòng tạo trước Tuần 8: 0/0/null.
//     public int EstimatedExpansionCount { get; set; }
//     public decimal EstimatedExpansionBudget { get; set; }   // VND
//     public string? ExpansionBreakdownJson { get; set; }     // object { additionalHeadcount, items[] } (JSON camelCase)
//
//     // Xoá mềm: "xoá" trên giao diện chỉ đặt cờ, dòng vẫn nằm trong DB (bị ẩn bởi query filter trong AppDbContext).
//     public bool IsDeleted { get; set; }
//     public DateTime? DeletedAt { get; set; }
//
//     public Department Department { get; set; } = null!;
// }
