using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.DTOs.Common;

namespace ITAM.API.Services.Interfaces;

public interface IAssetAllocationService
{
    Task<AssetAllocationResponseDto> CreateAsync(
        CreateAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId,
        int? currentUserId = null);

    Task<AssetAllocationResponseDto> ReturnAsync(
        int id,
        ReturnAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId,
        int? currentUserId = null);

    Task<PagedResultDto<AssetAllocationResponseDto>> GetPagedAsync(
        int? departmentId,
        string? status,
        int page,
        int pageSize,
        string? currentUserRole,
        int? currentUserDepartmentId,
        string? keyword = null,
        int? employeeId = null);

    Task<AssetAllocationResponseDto> GetByIdAsync(
        int id,
        string? currentUserRole,
        int? currentUserDepartmentId);

    // Dữ liệu in biên bản theo mẫu. kind: "handover" (mặc định) hoặc "return" (chỉ khi đã thu hồi).
    Task<AllocationDocumentDto> GetDocumentAsync(
        int id,
        string? kind,
        string? currentUserRole,
        int? currentUserDepartmentId);

    Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        int thresholdDays,
        string? currentUserRole,
        int? currentUserDepartmentId);
}
