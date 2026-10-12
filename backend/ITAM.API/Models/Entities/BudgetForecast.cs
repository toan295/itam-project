namespace ITAM.API.Models.Entities;

public class BudgetForecast
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public int EstimatedReplacementCount { get; set; }
    /// <summary>Ngân sách của phần THAY THẾ mà thôi (E8). Tổng = EstimatedBudget + EstimatedExpansionBudget.</summary>
    public decimal EstimatedBudget { get; set; }
    public string? Notes { get; set; }
    public string? BreakdownJson { get; set; }   // chi tiết theo loại tài sản (JSON camelCase)
    public DateTime? GeneratedAt { get; set; }   // lần tạo/cập nhật dự báo gần nhất (UTC)

    // Phần MUA THÊM theo kế hoạch mở rộng phòng ban (Tuần 8, E8). Dòng tạo trước Tuần 8: 0/0/null.
    public int EstimatedExpansionCount { get; set; }
    public decimal EstimatedExpansionBudget { get; set; }   // VND
    public string? ExpansionBreakdownJson { get; set; }     // object { additionalHeadcount, items[] } (JSON camelCase)

    // Xoá mềm: "xoá" trên giao diện chỉ đặt cờ, dòng vẫn nằm trong DB (bị ẩn bởi query filter trong AppDbContext).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Department Department { get; set; } = null!;
}
