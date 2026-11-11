namespace ITAM.API.Models.DTOs.Forecasts;

public class UpsertDeviceRatioRequestDto
{
    public decimal DevicesPerPerson { get; set; }
    public int? Version { get; set; }   // xem UpsertReferencePriceRequestDto.Version
}
