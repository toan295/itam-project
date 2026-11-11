using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Services.Interfaces;

// UC-17: kế hoạch mở rộng nhân sự theo phòng ban + định mức thiết bị/người (đầu vào của dự báo mua thêm).
// Sửa/xoá cần version đang thấy (409 khi lệch); tạo mới gửi version = null.
public interface IExpansionPlanningService
{
    Task<List<ExpansionPlanDto>> GetPlansAsync(int year, int? departmentId);
    Task<ExpansionPlanDto> UpsertPlanAsync(int year, int departmentId, UpsertExpansionPlanRequestDto dto, int? actorUserId = null);
    Task DeletePlanAsync(int year, int departmentId, int? version = null);

    Task<List<DeviceRatioDto>> GetRatiosAsync();
    Task<DeviceRatioDto> UpsertRatioAsync(int categoryId, UpsertDeviceRatioRequestDto dto, int? actorUserId = null);
    Task DeleteRatioAsync(int categoryId, int? version = null);
}
