namespace ITAM.API.Models.DTOs.Forecasts;

public class UpsertReferencePriceRequestDto
{
    public decimal UnitPrice { get; set; }

    // null = "loại này chưa có đơn giá, tôi tạo mới"; có giá trị = "tôi đang sửa đúng bản này". Lệch trạng thái hiện tại -> 409.
    public int? Version { get; set; }
}
