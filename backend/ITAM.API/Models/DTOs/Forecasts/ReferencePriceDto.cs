namespace ITAM.API.Models.DTOs.Forecasts;

public class ReferencePriceDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal? UnitPrice { get; set; }    // null = chưa cấu hình
}
