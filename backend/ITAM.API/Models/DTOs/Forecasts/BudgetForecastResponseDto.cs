namespace ITAM.API.Models.DTOs.Forecasts;

public class BudgetForecastResponseDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int EstimatedReplacementCount { get; set; }   // đếm TẤT CẢ tài sản đề xuất (kể cả loại thiếu giá)
    public decimal EstimatedBudget { get; set; }         // chỉ cộng các loại có đơn giá (VND)
    public string? Notes { get; set; }                   // tóm tắt cảnh báo thiếu đơn giá (nếu có)
    public DateTime? GeneratedAt { get; set; }
    public List<ForecastBreakdownItemDto> Breakdown { get; set; } = new();
}
