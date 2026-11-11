using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Models;

namespace ITAM.API.Repositories.Interfaces;

public interface ISoftwareLicenseRepository
{
    Task<(IReadOnlyList<SoftwareLicenseSnapshot> Items, int TotalItems)> GetPagedAsync(
        int page,
        int pageSize,
        SoftwareLicenseFilter filter);

    Task<SoftwareLicenseSnapshot?> GetByIdAsync(int id);
    Task<SoftwareLicense?> GetEntityByIdAsync(int id);
    Task<IReadOnlyList<SoftwareLicenseSnapshot>> GetExpiringSoonAsync(DateOnly fromDate, DateOnly toDate);
    Task<bool> LicenseKeyExistsAsync(string licenseKey, int? excludeId = null);
    Task<bool> AssetExistsAsync(int assetId);
    Task<bool> IsAssetDisposedAsync(int assetId); // tài sản đã thanh lý không được gán license.
    Task<bool> IsAssignedAsync(int licenseId, int assetId);

    // Các tài sản đang được gán license này (mã tài sản tăng dần).
    Task<IReadOnlyList<AssetSoftwareLicense>> GetAssignmentsAsync(int licenseId);
    Task<int> GetCurrentUsageAsync(int licenseId);
    Task AddAsync(SoftwareLicense license);
    Task UpdateAsync(SoftwareLicense license);
    Task DeleteAsync(SoftwareLicense license);
    Task AssignAsync(AssetSoftwareLicense assignment);
    Task<bool> UnassignAsync(int licenseId, int assetId);
}
