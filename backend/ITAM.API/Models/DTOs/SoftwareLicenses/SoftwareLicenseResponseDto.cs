namespace ITAM.API.Models.DTOs.SoftwareLicenses;

public class SoftwareLicenseResponseDto
{
    public int Id { get; set; }
    public string SoftwareName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public int CurrentUsage { get; set; }
    public decimal UsagePercentage { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    // Gần giới hạn: dùng từ 80% trở lên nhưng CHƯA hết chỗ (hết chỗ là IsFull).
    public bool IsNearUsageLimit { get; set; }
    public bool IsFull { get; set; }
    public int RemainingSlots { get; set; }

    // Trạng thái tổng hợp theo thứ tự ưu tiên: Expired > Full > NearLimit > ExpiringSoon > Active.
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}
