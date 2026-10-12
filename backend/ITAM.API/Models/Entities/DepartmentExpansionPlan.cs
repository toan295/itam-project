namespace ITAM.API.Models.Entities;

// Kế hoạch mở rộng nhân sự theo (năm, phòng ban) — đầu vào tính nhu cầu mua thêm thiết bị (UC-17, E1–E2).
public class DepartmentExpansionPlan
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public int AdditionalHeadcount { get; set; }   // số nhân sự tăng thêm trong CHÍNH năm đó (không cộng dồn), 0..5000
    public string? Notes { get; set; }             // tối đa 500 ký tự
    public DateTime UpdatedAt { get; set; }        // UTC, luôn gán khi tạo/sửa

    public Department Department { get; set; } = null!;
}
