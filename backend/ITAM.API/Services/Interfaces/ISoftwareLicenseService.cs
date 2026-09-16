using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.SoftwareLicenses;

namespace ITAM.API.Services.Interfaces;

public interface ISoftwareLicenseService
{
    Task<PagedResultDto<SoftwareLicenseResponseDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null);

    Task<SoftwareLicenseResponseDto> GetByIdAsync(int id);
    Task<SoftwareLicenseResponseDto> CreateAsync(CreateSoftwareLicenseDto dto);
    Task<SoftwareLicenseResponseDto> UpdateAsync(int id, UpdateSoftwareLicenseDto dto);
    Task DeleteAsync(int id);
    Task<SoftwareLicenseResponseDto> AssignAsync(int id, int assetId);
    Task UnassignAsync(int id, int assetId);
    Task<IReadOnlyList<SoftwareLicenseResponseDto>> GetExpiringSoonAsync(int days = 30);
}
