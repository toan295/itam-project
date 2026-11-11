namespace ITAM.API.Models.DTOs.Forecasts;

public class DeviceRatioDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal? DevicesPerPerson { get; set; }   // null = chưa cấu hình (không tính mua thêm, E4)
    public int? Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
}
