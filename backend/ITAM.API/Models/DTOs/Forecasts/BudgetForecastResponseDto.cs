namespace ITAM.API.Models.DTOs.Forecasts;

public class BudgetForecastResponseDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int EstimatedReplacementCount { get; set; }   // đếm TẤT CẢ tài sản đề xuất (kể cả loại thiếu giá)
    /// <summary>Ngân sách của phần THAY THẾ mà thôi (chỉ loại có đơn giá, VND). Muốn tổng dùng <see cref="TotalBudget"/>.</summary>
    public decimal EstimatedBudget { get; set; }
    public string? Notes { get; set; }                   // tóm tắt cảnh báo thiếu đơn giá (nếu có)
    public DateTime? GeneratedAt { get; set; }
    public List<ForecastBreakdownItemDto> Breakdown { get; set; } = new();

    // ---- Phần mua thêm theo kế hoạch mở rộng phòng ban (Tuần 8) ----
    public int AdditionalHeadcount { get; set; }                 // nhân sự tăng thêm đã dùng khi tạo dự báo (0 nếu không có kế hoạch/dòng cũ)
    public int EstimatedExpansionCount { get; set; }
    public decimal EstimatedExpansionBudget { get; set; }
    public List<ExpansionBreakdownItemDto> ExpansionBreakdown { get; set; } = new();
    public int TotalPurchaseCount { get; set; }                  // = EstimatedReplacementCount + EstimatedExpansionCount (tính khi map)
    public decimal TotalBudget { get; set; }                     // = EstimatedBudget + EstimatedExpansionBudget (tính khi map)
    public List<PurchaseSummaryItemDto> PurchaseSummary { get; set; } = new();
}
