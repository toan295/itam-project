namespace ITAM.API.Models.DTOs.Lifecycle;

// Danh sách "sắp tới hạn" (B2): tài sản CHƯA vượt ngưỡng nhưng sắp đủ tuổi hoặc sát ngưỡng lỗi.
public class UpcomingReplacementDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public double? AgeYears { get; set; }
    public int TicketCount { get; set; }
    public DateOnly? AgeDueDate { get; set; }        // PurchaseDate + ngưỡng tuổi hiệu lực; null khi không có PurchaseDate
    public int? DaysUntilAgeDue { get; set; }        // chỉ có giá trị khi AgeDueSoon (ngày đủ tuổi nằm trong cửa sổ); ngược lại null
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();   // chỉ "AgeDueSoon"/"NearFailureThreshold" — không dịch
    public string ReasonText { get; set; } = string.Empty;
}
