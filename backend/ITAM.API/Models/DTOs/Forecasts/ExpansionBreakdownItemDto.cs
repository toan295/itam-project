namespace ITAM.API.Models.DTOs.Forecasts;

// Một loại tài sản trong phần MUA THÊM của một phòng ban. Quantity là PHẦN THIẾU HỤT sau khi trừ tồn kho dùng được:
//   Demand   = ⌈ProjectedHeadcount × DevicesPerPerson⌉
//   Quantity = max(0, Demand − ExistingUsable)
// Thiết bị đến hạn thay thế được thay 1:1 ở phần THAY THẾ nên vẫn giữ nguyên năng lực (không trừ vào mua thêm, cũng không bị đếm hai lần).
public class ExpansionBreakdownItemDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal DevicesPerPerson { get; set; }
    public int Demand { get; set; }                // nhu cầu của phòng ban sau điều chỉnh nhân sự
    public int ExistingUsable { get; set; }        // tài sản cùng loại, cùng phòng ban, đang dùng được (InUse/Maintenance; không tính Broken/Disposed)
    public int ReplacementDue { get; set; }        // trong số đó (hoặc hỏng) đến hạn thay thế trong năm — được tính ở phần thay thế
    public int Surplus { get; set; }               // max(0, ExistingUsable − Demand)
    public int ReplacementPossiblyAvoidable { get; set; }   // min(ReplacementDue, Surplus): CHỈ gợi ý, KHÔNG tự trừ khỏi phần thay thế
    public int Quantity { get; set; }              // = max(0, Demand − ExistingUsable)
    public decimal? UnitPrice { get; set; }        // null = chưa có đơn giá (E5)
    public decimal? Subtotal { get; set; }         // null khi UnitPrice null; ngược lại Quantity * UnitPrice
    public string Explanation { get; set; } = string.Empty;
}
