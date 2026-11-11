namespace ITAM.API.Models.DTOs.Lifecycle;

// Giải trình điểm ưu tiên để Admin tự tính lại được: điểm = làm tròn(50 × max(tuổi/ngưỡng tuổi, số phiếu/ngưỡng phiếu)), kẹp 0–100.
// Đúng ngưỡng ⇒ 50; gấp đôi ngưỡng trở lên ⇒ 100. Hai tài sản khác điểm vì khác tỷ lệ vượt ngưỡng của thành phần lớn hơn.
public class PriorityScoreBreakdownDto
{
    public double? AgeYears { get; set; }
    public int MaxAgeYears { get; set; }
    public double AgeRatio { get; set; }          // AgeYears / MaxAgeYears (0 khi thiếu/không hợp lệ ngày mua)
    public int TicketCount { get; set; }
    public int MaxFailureCount { get; set; }
    public double FailureRatio { get; set; }      // TicketCount / MaxFailureCount
    public double Severity { get; set; }          // max(AgeRatio, FailureRatio)
    public int Score { get; set; }                // 0..100
    public string DrivenBy { get; set; } = "None";   // "Age" | "Failures" | "Both" | "None": thành phần quyết định điểm
}
