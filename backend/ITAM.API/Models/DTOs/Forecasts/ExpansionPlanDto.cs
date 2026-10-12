namespace ITAM.API.Models.DTOs.Forecasts;

public class ExpansionPlanDto
{
    public int Year { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int AdditionalHeadcount { get; set; }     // 0 khi chưa có kế hoạch
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }         // null khi chưa có kế hoạch
    public bool HasPlan { get; set; }
}
