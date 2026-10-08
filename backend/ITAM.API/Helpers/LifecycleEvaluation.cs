namespace ITAM.API.Helpers;

// Kết quả đánh giá vòng đời của một tài sản (nội bộ — không trả thẳng ra API).
public class LifecycleEvaluation
{
    // null = chưa có căn cứ để dự báo (không có PurchaseDate và chưa vượt ngưỡng lỗi).
    public int? DueYear { get; set; }
    public bool IsOverdueNow { get; set; }
    public bool IsHighPriority { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
