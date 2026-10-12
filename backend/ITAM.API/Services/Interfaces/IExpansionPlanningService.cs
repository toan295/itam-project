using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Services.Interfaces;

// UC-17: kế hoạch mở rộng nhân sự theo phòng ban + định mức thiết bị/người (đầu vào của dự báo mua thêm).
public interface IExpansionPlanningService
{
    Task<List<ExpansionPlanDto>> GetPlansAsync(int year, int? departmentId);
    Task<ExpansionPlanDto> UpsertPlanAsync(int year, int departmentId, UpsertExpansionPlanRequestDto dto);
    Task DeletePlanAsync(int year, int departmentId);

    Task<List<DeviceRatioDto>> GetRatiosAsync();
    Task<DeviceRatioDto> UpsertRatioAsync(int categoryId, UpsertDeviceRatioRequestDto dto);
    Task DeleteRatioAsync(int categoryId);
}
