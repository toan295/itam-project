using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _db;

    public AssetRepository(AppDbContext db)
    {
        _db = db;
    }

    // AsNoTracking an toàn ở đây dù kết quả có thể bị sửa rồi lưu lại (UpdateAsync/DisposeAsync):
    // Update(asset) bên dưới luôn gọi _db.Assets.Update(asset) tường minh, tự attach + đánh dấu
    // Modified bất kể entity có đang được track hay không — không phụ thuộc change-tracking ngầm.
    public Task<Asset?> GetByIdWithDetailsAsync(int id) =>
        _db.Assets
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<Asset?> GetByAssetCodeAsync(string assetCode) =>
        _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetCode == assetCode);

    public async Task AddAsync(Asset asset) => await _db.Assets.AddAsync(asset);

    public void Update(Asset asset) => _db.Assets.Update(asset);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<bool> CategoryExistsAsync(int categoryId) =>
        _db.AssetCategories.AnyAsync(c => c.Id == categoryId);

    public Task<bool> DepartmentExistsAsync(int departmentId) =>
        _db.Departments.AnyAsync(d => d.Id == departmentId);

    public async Task<(List<Asset> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, AssetStatus? status, int page, int pageSize)
    {
        var query = _db.Assets.AsNoTracking().Include(a => a.Category).Include(a => a.Department).AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(a => a.DepartmentId == departmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Asset> Items, int TotalItems)> SearchAsync(
        string? keyword,
        int? departmentId,
        int? categoryId,
        AssetStatus? status,
        int? purchaseYear,
        bool? isUnderWarranty,
        int page,
        int pageSize)
    {
        var query = _db.Assets.AsNoTracking().Include(a => a.Category).Include(a => a.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(a => a.Name.Contains(keyword) || a.AssetCode.Contains(keyword));
        }

        if (departmentId.HasValue)
        {
            query = query.Where(a => a.DepartmentId == departmentId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (purchaseYear.HasValue)
        {
            query = query.Where(a => a.PurchaseDate != null && a.PurchaseDate.Value.Year == purchaseYear.Value);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (isUnderWarranty == true)
        {
            query = query.Where(a => a.WarrantyExpiry != null && a.WarrantyExpiry.Value >= today);
        }
        else if (isUnderWarranty == false)
        {
            query = query.Where(a => a.WarrantyExpiry != null && a.WarrantyExpiry.Value < today);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.PurchaseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
