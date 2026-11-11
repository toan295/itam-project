namespace ITAM.API.Models.DTOs.Lifecycle;

public class LifecyclePolicyDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }
    public bool IsOverridden { get; set; }

    // Nguồn của ngưỡng đang áp dụng: "Default" (mặc định hệ thống) hoặc "Custom" (Admin đã đặt riêng cho loại này).
    // Ngưỡng luôn CÓ HIỆU LỰC ngay khi lưu (không có trạng thái nháp/khoá); IsOverridden giữ lại cho tương thích.
    public string Source { get; set; } = "Default";
    public int DefaultMaxAgeYears { get; set; }
    public int DefaultMaxFailureCount { get; set; }
    public int? Version { get; set; }                 // null khi đang dùng mặc định (chưa có dòng riêng)
    public DateTime? UpdatedAt { get; set; }          // UTC; null khi đang dùng mặc định
    public int? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
}
