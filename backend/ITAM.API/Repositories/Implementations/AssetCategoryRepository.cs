using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AssetCategoryRepository : IAssetCategoryRepository
{
    private readonly AppDbContext _db;

    public AssetCategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<AssetCategory>> GetAllAsync() =>
        _db.AssetCategories.AsNoTracking().OrderBy(c => c.Id).ToListAsync(); // Sắp theo Id: thứ tự ổn định, dễ đối chiếu.

    // AsNoTracking an toàn dù kết quả có thể bị sửa/xoá rồi lưu lại (UpdateAsync/DeleteAsync) —
    // Update()/Remove() bên dưới luôn gọi tường minh, không phụ thuộc change-tracking ngầm.
    public Task<AssetCategory?> GetByIdAsync(int id) =>
        _db.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public Task<AssetCategory?> GetByNameAsync(string name) =>
        _db.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Name == name);

    public async Task AddAsync(AssetCategory category) => await _db.AssetCategories.AddAsync(category);

    public void Update(AssetCategory category) => _db.AssetCategories.Update(category);

    public void Remove(AssetCategory category) => _db.AssetCategories.Remove(category);

    public Task<bool> IsReferencedByAssetsAsync(int categoryId) =>
        _db.Assets.AnyAsync(a => a.CategoryId == categoryId);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
