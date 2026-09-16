using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IAssetCategoryRepository
{
    Task<List<AssetCategory>> GetAllAsync();
    Task<AssetCategory?> GetByIdAsync(int id);
    Task<AssetCategory?> GetByNameAsync(string name);
    Task AddAsync(AssetCategory category);
    void Update(AssetCategory category);
    void Remove(AssetCategory category);
    Task<bool> IsReferencedByAssetsAsync(int categoryId);
    Task<int> SaveChangesAsync();
}
