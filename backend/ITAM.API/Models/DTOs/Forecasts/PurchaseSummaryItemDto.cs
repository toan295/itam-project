namespace ITAM.API.Models.DTOs.Forecasts;

// "Danh sách mua sắm": gộp thay thế + mua thêm theo loại. Tính khi map DTO, KHÔNG lưu DB.
public class PurchaseSummaryItemDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int ReplacementQuantity { get; set; }
    public int ExpansionQuantity { get; set; }
    public int TotalQuantity { get; set; }         // = ReplacementQuantity + ExpansionQuantity
    public decimal? UnitPrice { get; set; }
    public decimal? TotalCost { get; set; }        // null khi UnitPrice null; ngược lại TotalQuantity * UnitPrice
}
