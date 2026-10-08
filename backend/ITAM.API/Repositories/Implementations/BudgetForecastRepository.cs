using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class BudgetForecastRepository : IBudgetForecastRepository
{
    private readonly AppDbContext _db;

    public BudgetForecastRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(List<BudgetForecast> Items, int TotalItems)> GetPagedAsync(
        int? year, int? departmentId, int page, int pageSize)
    {
        var query = _db.BudgetForecasts.AsNoTracking().Include(f => f.Department).AsQueryable();
        if (year.HasValue) query = query.Where(f => f.Year == year.Value);
        if (departmentId.HasValue) query = query.Where(f => f.DepartmentId == departmentId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.Year)
            .ThenBy(f => f.Department.Name)
            .ThenBy(f => f.Id)   // thứ tự ổn định khi phân trang
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<BudgetForecast?> GetByIdWithDetailsAsync(int id) =>
        _db.BudgetForecasts.AsNoTracking().Include(f => f.Department).FirstOrDefaultAsync(f => f.Id == id);

    // Có tracking: Service sửa trực tiếp entity này rồi SaveChanges (upsert theo Năm + Phòng ban).
    // IgnoreQueryFilters: dòng đã xoá mềm vẫn chiếm chỗ ở unique index (Năm, Phòng ban) nên phải tìm thấy để khôi phục.
    public Task<BudgetForecast?> GetByYearAndDepartmentAsync(int year, int departmentId) =>
        _db.BudgetForecasts.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Year == year && f.DepartmentId == departmentId);

    public Task<BudgetForecast?> GetTrackedByIdAsync(int id) =>
        _db.BudgetForecasts.FirstOrDefaultAsync(f => f.Id == id);

    public async Task AddAsync(BudgetForecast forecast) => await _db.BudgetForecasts.AddAsync(forecast);

    public void Update(BudgetForecast forecast) => _db.BudgetForecasts.Update(forecast);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public async Task<List<(int Id, string Name)>> GetAllDepartmentsAsync() =>
        (await _db.Departments.AsNoTracking().OrderBy(d => d.Id).Select(d => new { d.Id, d.Name }).ToListAsync())
            .Select(d => (d.Id, d.Name)).ToList();

    public async Task<(int Id, string Name)?> GetDepartmentAsync(int id)
    {
        var d = await _db.Departments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync();
        return d is null ? null : (d.Id, d.Name);
    }

    public Task<List<AssetCategoryReferencePrice>> GetAllPricesAsync() =>
        _db.AssetCategoryReferencePrices.AsNoTracking().ToListAsync();

    // Gồm cả dòng đã xoá mềm (PK = CategoryId nên không thể thêm dòng thứ hai) — Service tự xử lý cờ IsDeleted.
    public Task<AssetCategoryReferencePrice?> GetPriceAsync(int categoryId) =>
        _db.AssetCategoryReferencePrices.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.CategoryId == categoryId);

    public async Task AddPriceAsync(AssetCategoryReferencePrice price) =>
        await _db.AssetCategoryReferencePrices.AddAsync(price);

    public void UpdatePrice(AssetCategoryReferencePrice price) =>
        _db.AssetCategoryReferencePrices.Update(price);

    public async Task<List<(int Id, string Name)>> GetAllCategoriesAsync() =>
        (await _db.AssetCategories.AsNoTracking().OrderBy(c => c.Id).Select(c => new { c.Id, c.Name }).ToListAsync())
            .Select(c => (c.Id, c.Name)).ToList();

    public Task<bool> CategoryExistsAsync(int categoryId) =>
        _db.AssetCategories.AnyAsync(c => c.Id == categoryId);
}
