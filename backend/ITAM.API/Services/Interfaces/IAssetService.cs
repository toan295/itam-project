using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.DTOs.Common;

namespace ITAM.API.Services.Interfaces;

public interface IAssetService
{
    Task<AssetResponseDto> CreateAsync(CreateAssetRequestDto dto, string? currentUserRole, int? currentUserDepartmentId);

    Task<AssetResponseDto> UpdateAsync(
        int id, UpdateAssetRequestDto dto, string? currentUserRole, int? currentUserDepartmentId);

    // "Xoá" = chuyển Status sang Disposed, không hard-delete (UC-07).
    Task<AssetResponseDto> DisposeAsync(int id);

    // UC-08 E2: cùng phạm vi phòng ban với GetPagedAsync/SearchAsync — Manager/Technician không
    // được xem chi tiết tài sản ngoài phòng ban chỉ bằng cách đoán Id (tránh IDOR).
    Task<AssetResponseDto> GetByIdAsync(int id, string? currentUserRole, int? currentUserDepartmentId);

    // UC-08 E2: Manager/Technician tự động bị giới hạn theo phòng ban mình phụ trách.
    Task<PagedResultDto<AssetResponseDto>> GetPagedAsync(
        int? departmentId, string? status, int page, int pageSize,
        string? currentUserRole, int? currentUserDepartmentId);

    Task<PagedResultDto<AssetResponseDto>> SearchAsync(
        AssetSearchFilterDto filter, string? currentUserRole, int? currentUserDepartmentId);
}
