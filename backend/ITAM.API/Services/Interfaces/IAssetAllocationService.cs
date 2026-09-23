using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.DTOs.Common;

namespace ITAM.API.Services.Interfaces;

public interface IAssetAllocationService
{
    Task<AssetAllocationResponseDto> CreateAsync(
        CreateAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId);

    Task<AssetAllocationResponseDto> ReturnAsync(
        int id,
        ReturnAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId);

    Task<PagedResultDto<AssetAllocationResponseDto>> GetPagedAsync(
        int? departmentId,
        string? status,
        int page,
        int pageSize,
        string? currentUserRole,
        int? currentUserDepartmentId);

    Task<AssetAllocationResponseDto> GetByIdAsync(
        int id,
        string? currentUserRole,
        int? currentUserDepartmentId);

    Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        int thresholdDays,
        string? currentUserRole,
        int? currentUserDepartmentId);
}
