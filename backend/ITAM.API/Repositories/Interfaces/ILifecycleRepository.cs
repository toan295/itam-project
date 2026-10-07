using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface ILifecycleRepository
{
    Task<IReadOnlyList<AssetLifecycleRow>> GetAssetLifecycleRowsAsync(int? departmentId);
    Task<IReadOnlyList<AssetCategoryLifecyclePolicy>> GetPoliciesAsync();
    Task<IReadOnlyList<AssetCategory>> GetCategoriesAsync();
    Task<AssetCategory?> GetCategoryByIdAsync(int categoryId);
    Task<AssetCategoryLifecyclePolicy?> GetPolicyByCategoryIdAsync(int categoryId);
    Task AddPolicyAsync(AssetCategoryLifecyclePolicy policy);
    void RemovePolicy(AssetCategoryLifecyclePolicy policy);
    Task<int> SaveChangesAsync();
}
