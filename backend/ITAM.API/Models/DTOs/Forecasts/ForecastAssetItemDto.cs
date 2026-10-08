namespace ITAM.API.Models.DTOs.Forecasts;

// Một tài sản được đề xuất thay thế trong năm dự báo — lưu cùng BreakdownJson để giải thích "vì sao" con số được tính ra.
public class ForecastAssetItemDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public DateOnly? PurchaseDate { get; set; }
    public double? AgeYears { get; set; }
    public int TicketCount { get; set; }
    public int MaxAgeYears { get; set; }        // ngưỡng đã áp dụng lúc tính (để giải thích lý do)
    public int MaxFailureCount { get; set; }
    public List<string> Reasons { get; set; } = new();   // "AgeExceeded" / "FailureCountExceeded"
}
