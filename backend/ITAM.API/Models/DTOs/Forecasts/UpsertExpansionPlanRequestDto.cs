namespace ITAM.API.Models.DTOs.Forecasts;

public class UpsertExpansionPlanRequestDto
{
    // Thay đổi RÒNG nhân sự của năm: dương = tăng, âm = giảm (-5000..5000).
    public int AdditionalHeadcount { get; set; }
    public string? Notes { get; set; }
    public int? Version { get; set; }   // xem UpsertReferencePriceRequestDto.Version
}
