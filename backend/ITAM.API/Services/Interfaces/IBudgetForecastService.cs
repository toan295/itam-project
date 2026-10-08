using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Services.Interfaces;

public interface IBudgetForecastService
{
    Task<ForecastGenerationResultDto> GenerateAsync(GenerateForecastRequestDto dto);
    Task<PagedResultDto<BudgetForecastResponseDto>> GetPagedAsync(int? year, int? departmentId, int page, int pageSize);
    Task<BudgetForecastResponseDto> GetByIdAsync(int id);
    Task DeleteAsync(int id);

    Task<List<ReferencePriceDto>> GetPricesAsync();
    Task<ReferencePriceDto> UpsertPriceAsync(int categoryId, UpsertReferencePriceRequestDto dto);
    Task DeletePriceAsync(int categoryId);
}
