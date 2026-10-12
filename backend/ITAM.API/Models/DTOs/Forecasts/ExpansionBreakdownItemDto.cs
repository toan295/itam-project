namespace ITAM.API.Models.DTOs.Forecasts;

public class ExpansionBreakdownItemDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal DevicesPerPerson { get; set; }
    public int Quantity { get; set; }              // ⌈AdditionalHeadcount × DevicesPerPerson⌉ (E1)
    public decimal? UnitPrice { get; set; }        // null = chưa có đơn giá (E5)
    public decimal? Subtotal { get; set; }         // null khi UnitPrice null; ngược lại Quantity * UnitPrice
}
