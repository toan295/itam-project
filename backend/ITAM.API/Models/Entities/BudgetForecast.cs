namespace ITAM.API.Models.Entities;

public class BudgetForecast
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public int EstimatedReplacementCount { get; set; }
    public decimal EstimatedBudget { get; set; }
    public string? Notes { get; set; }
    public string? BreakdownJson { get; set; }   // chi tiết theo loại tài sản (JSON camelCase)
    public DateTime? GeneratedAt { get; set; }   // lần tạo/cập nhật dự báo gần nhất (UTC)

    // Xoá mềm: "xoá" trên giao diện chỉ đặt cờ, dòng vẫn nằm trong DB (bị ẩn bởi query filter trong AppDbContext).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Department Department { get; set; } = null!;
}
