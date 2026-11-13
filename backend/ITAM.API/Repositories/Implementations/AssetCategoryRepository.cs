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

    // Xoá mềm: giữ dòng trong CSDL, chỉ đánh dấu đã xoá.
    public void Remove(AssetCategory category)
    {
        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        _db.AssetCategories.Update(category);

        // Cấu hình gắn với loại này (ngưỡng vòng đời, định mức) trước đây bị xoá theo (cascade); giữ hành vi đó để không còn
        // cấu hình mồ côi trỏ vào loại đã xoá. Đơn giá tham khảo vốn dùng xoá mềm nên chỉ đánh dấu.
        _db.AssetCategoryLifecyclePolicies.RemoveRange(_db.AssetCategoryLifecyclePolicies.Where(x => x.CategoryId == category.Id).ToList());
        // [ĐÃ ĐÓNG]
        // _db.AssetCategoryDeviceRatios.RemoveRange(_db.AssetCategoryDeviceRatios.Where(x => x.CategoryId == category.Id).ToList());
        // foreach (var price in _db.AssetCategoryReferencePrices.Where(x => x.CategoryId == category.Id).ToList())
        // {
            // price.IsDeleted = true;
            // price.DeletedAt = DateTime.UtcNow;
        // }
    }

    public Task<bool> IsReferencedByAssetsAsync(int categoryId) =>
        _db.Assets.AnyAsync(a => a.CategoryId == categoryId);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
