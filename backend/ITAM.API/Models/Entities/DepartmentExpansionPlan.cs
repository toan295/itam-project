namespace ITAM.API.Models.Entities;

// Kế hoạch mở rộng nhân sự theo (năm, phòng ban) — đầu vào tính nhu cầu mua thêm thiết bị (UC-17, E1–E2).
public class DepartmentExpansionPlan
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    // Thay đổi RÒNG nhân sự trong CHÍNH năm đó (không cộng dồn): dương = tăng, âm = giảm, -5000..5000. Tăng 5 rồi giảm 3 trong năm = nhập +2.
    public int AdditionalHeadcount { get; set; }
    public string? Notes { get; set; }             // tối đa 500 ký tự
    public DateTime UpdatedAt { get; set; }        // UTC, luôn gán khi tạo/sửa
    public int? UpdatedByUserId { get; set; }
    public int Version { get; set; } = 1;          // optimistic concurrency, tự tăng khi sửa

    public Department Department { get; set; } = null!;
}
