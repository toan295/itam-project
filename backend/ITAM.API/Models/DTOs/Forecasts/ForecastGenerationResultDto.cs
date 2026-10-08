namespace ITAM.API.Models.DTOs.Forecasts;

public class ForecastGenerationResultDto
{
    public int Year { get; set; }
    public List<BudgetForecastResponseDto> Forecasts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
