namespace ITAM.API.Models.DTOs.Forecasts;

public class ExpansionPlanDto
{
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int AdditionalHeadcount { get; set; }     // thay đổi ròng theo kế hoạch (âm = giảm); 0 khi chưa có kế hoạch
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }         // null khi chưa có kế hoạch
    public bool HasPlan { get; set; }
    public int? Version { get; set; }
    public string? UpdatedByName { get; set; }
    public int BaselineHeadcount { get; set; }       // nhân viên đang hoạt động của phòng ban HIỆN TẠI (bảng Nhân viên)
    public int ProjectedHeadcount { get; set; }      // max(0, baseline + AdditionalHeadcount) — số dùng để tính nhu cầu
}
