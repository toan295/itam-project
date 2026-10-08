namespace ITAM.API.Models.DTOs.Forecasts;

public class GenerateForecastRequestDto
{
    public int Year { get; set; }
    public int? DepartmentId { get; set; }     // null = mọi phòng ban (D8)
}
