namespace ITAM.API.Models.DTOs.Lifecycle;

// HỢP ĐỒNG giữa module Vòng đời (UC-16) và module Dự báo ngân sách (UC-17) — ke-hoach-tuan7-lan2.md, Mục C.1.
// Đổi tên/ý nghĩa trường nào ở đây phải báo module Dự báo trước: đổi âm thầm làm dự báo sai mà không lỗi build.
public class AssetReplacementCandidateDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public DateOnly? PurchaseDate { get; set; }
    public double? AgeYears { get; set; }            // null khi không có PurchaseDate
    public int TicketCount { get; set; }
    public int FailedTicketCount { get; set; }
    public double FailureRatePerYear { get; set; }   // TicketCount / Math.Max(AgeYears ?? 0, 1.0)
    public int MaxAgeYears { get; set; }             // ngưỡng hiệu lực đã áp dụng cho tài sản này (thông tin thêm)
    public int MaxFailureCount { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();   // chỉ "AgeExceeded"/"FailureCountExceeded" — KHÔNG dịch
    public bool IsOverdueNow { get; set; }           // đã vượt ngưỡng tại thời điểm gọi
    public bool IsHighPriority { get; set; }         // vượt cả 2 ngưỡng
    public int DueYear { get; set; }                 // luôn có giá trị và luôn >= năm hiện tại
}
