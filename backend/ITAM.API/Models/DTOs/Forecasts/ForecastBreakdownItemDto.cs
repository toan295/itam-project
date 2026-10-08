namespace ITAM.API.Models.DTOs.Forecasts;

public class ForecastBreakdownItemDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int AssetCount { get; set; }
    public decimal? UnitPrice { get; set; }    // null = chưa cấu hình đơn giá (D7)
    public decimal? Subtotal { get; set; }     // null khi UnitPrice null; ngược lại = AssetCount * UnitPrice
    public List<ForecastAssetItemDto> Assets { get; set; } = new();   // tài sản cấu thành số lượng (dự báo cũ có thể rỗng)
}
