using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface ILifecycleRepository
{
    Task<IReadOnlyList<AssetLifecycleRow>> GetAssetLifecycleRowsAsync(int? departmentId);
    // M2: một tài sản (null nếu không tồn tại hoặc đã Disposed) cùng danh sách phiếu bảo trì, mới nhất trước.
    Task<AssetLifecycleRow?> GetAssetLifecycleRowAsync(int assetId);
    Task<IReadOnlyList<AssetTicketSummaryDto>> GetTicketsByAssetAsync(int assetId);
    Task<IReadOnlyList<TicketTrendPointDto>> GetTicketCountByYearAsync(int? departmentId, int fromYear);
    Task<IReadOnlyList<AssetCategoryLifecyclePolicy>> GetPoliciesAsync();
    Task<IReadOnlyList<AssetCategory>> GetCategoriesAsync();
    Task<AssetCategory?> GetCategoryByIdAsync(int categoryId);
    Task<AssetCategoryLifecyclePolicy?> GetPolicyByCategoryIdAsync(int categoryId);
    Task AddPolicyAsync(AssetCategoryLifecyclePolicy policy);
    void RemovePolicy(AssetCategoryLifecyclePolicy policy);
    Task<int> SaveChangesAsync();
}
