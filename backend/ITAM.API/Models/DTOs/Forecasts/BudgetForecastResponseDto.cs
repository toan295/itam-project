// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// namespace ITAM.API.Models.DTOs.Forecasts;
//
// public class BudgetForecastResponseDto
// {
//     public int Id { get; set; }
//     public int Year { get; set; }
//     public int DepartmentId { get; set; }
//     public string DepartmentName { get; set; } = null!;
//     public int EstimatedReplacementCount { get; set; }   // đếm TẤT CẢ tài sản đề xuất (kể cả loại thiếu giá)
//     /// <summary>Ngân sách của phần THAY THẾ mà thôi (chỉ loại có đơn giá, VND). Muốn tổng dùng <see cref="TotalBudget"/>.</summary>
//     public decimal EstimatedBudget { get; set; }
//     public string? Notes { get; set; }                   // tóm tắt cảnh báo thiếu đơn giá (nếu có)
//     public DateTime? GeneratedAt { get; set; }
//     public List<ForecastBreakdownItemDto> Breakdown { get; set; } = new();
//
//     // ---- Phần mua thêm theo kế hoạch mở rộng phòng ban (Tuần 8) ----
//     public int AdditionalHeadcount { get; set; }                 // thay đổi ròng nhân sự đã dùng khi tạo dự báo (âm = giảm; 0 nếu không có kế hoạch/dòng cũ)
//     public int BaselineHeadcount { get; set; }                   // nhân viên hoạt động của phòng ban TẠI THỜI ĐIỂM tạo dự báo (0 với dòng cũ)
//     public int ProjectedHeadcount { get; set; }                  // max(0, Baseline + Additional)
//     public int EstimatedExpansionCount { get; set; }
//     public decimal EstimatedExpansionBudget { get; set; }
//     public List<ExpansionBreakdownItemDto> ExpansionBreakdown { get; set; } = new();
//     public int TotalPurchaseCount { get; set; }                  // = EstimatedReplacementCount + EstimatedExpansionCount (tính khi map)
//     public decimal TotalBudget { get; set; }                     // = EstimatedBudget + EstimatedExpansionBudget (tính khi map)
//     public List<PurchaseSummaryItemDto> PurchaseSummary { get; set; } = new();
//
//     // ---- Tái lập & tính cập nhật ----
//     public int Revision { get; set; }
//     // "Current" = đầu vào hiện tại khớp lúc tạo; "Stale" = đầu vào đã đổi (xem StaleReasons) — số liệu vẫn là bản đã lưu;
//     // "Unknown" = dòng cũ không lưu đầu vào; "NotChecked" = không kiểm tra ở nơi trả về này (vd. ngay sau khi tạo).
//     public string StaleStatus { get; set; } = "NotChecked";
//     public List<string> StaleReasons { get; set; } = new();
//     // Chỉ có ở kết quả của lần "Tạo dự báo": "Created" | "Updated" | "Restored" | "Unchanged" và bản trước đó (nếu có).
//     public string? Change { get; set; }
//     public ForecastPreviousDto? Previous { get; set; }
// }
//
// public class ForecastPreviousDto
// {
//     public int Revision { get; set; }
//     public DateTime? GeneratedAt { get; set; }
//     public int TotalPurchaseCount { get; set; }
//     public decimal TotalBudget { get; set; }
// }
