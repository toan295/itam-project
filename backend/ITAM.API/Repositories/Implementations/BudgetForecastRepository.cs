using ITAM.API.Repositories.Models;
using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
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
        int? year, int? departmentId, int page, int pageSize, ForecastListExtras extras)
    {
        var query = _db.BudgetForecasts.AsNoTracking().Include(f => f.Department).AsQueryable();
        if (year.HasValue) query = query.Where(f => f.Year == year.Value);
        if (departmentId.HasValue) query = query.Where(f => f.DepartmentId == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(extras.Keyword))
        {
            var k = extras.Keyword.Trim();
            query = query.Where(f => f.Department.Name.Contains(k) || (f.Notes != null && f.Notes.Contains(k)));
        }

        if (extras.Kind == "replacement")
        {
            query = query.Where(f => f.EstimatedReplacementCount > 0);
        }
        else if (extras.Kind == "expansion")
        {
            query = query.Where(f => f.EstimatedExpansionCount > 0);
        }

        if (extras.MinBudget.HasValue)
        {
            query = query.Where(f => f.EstimatedBudget + f.EstimatedExpansionBudget >= extras.MinBudget.Value);
        }

        if (extras.MaxBudget.HasValue)
        {
            query = query.Where(f => f.EstimatedBudget + f.EstimatedExpansionBudget <= extras.MaxBudget.Value);
        }

        var total = await query.CountAsync();
        var ordered = extras.Sort switch
        {
            "budget" => query.OrderByDescending(f => f.EstimatedBudget + f.EstimatedExpansionBudget),
            "department" => query.OrderBy(f => f.Department.Name).ThenByDescending(f => f.Year),
            "generated" => query.OrderByDescending(f => f.GeneratedAt),
            _ => query.OrderByDescending(f => f.Year).ThenBy(f => f.Department.Name),
        };
        var items = await ordered
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
    public Task<List<BudgetForecast>> GetForecastsByYearAsync(int year) =>
        _db.BudgetForecasts.IgnoreQueryFilters().Where(f => f.Year == year).ToListAsync();

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

    public Task<Dictionary<int, int>> GetActiveEmployeeCountsAsync() =>
        _db.Employees.AsNoTracking().Where(e => e.IsActive)
            .GroupBy(e => e.DepartmentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    // InUse + Maintenance = đáp ứng được nhu cầu (bảo trì là tạm thời). Broken không đáp ứng được; Disposed đã rời hệ thống.
    public async Task<Dictionary<(int DepartmentId, int CategoryId), int>> GetUsableAssetCountsAsync() =>
        (await _db.Assets.AsNoTracking()
            .Where(a => a.Status == AssetStatus.InUse || a.Status == AssetStatus.Maintenance)
            .GroupBy(a => new { a.DepartmentId, a.CategoryId }).Select(g => new { g.Key.DepartmentId, g.Key.CategoryId, Count = g.Count() })
            .ToListAsync())
        .ToDictionary(x => (x.DepartmentId, x.CategoryId), x => x.Count);

    public Task<Dictionary<int, int>> GetAssetsWithoutPurchaseDateCountsAsync() =>
        _db.Assets.AsNoTracking().Where(a => a.Status != AssetStatus.Disposed && a.PurchaseDate == null)
            .GroupBy(a => a.DepartmentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return ids.Count == 0
            ? Task.FromResult(new Dictionary<int, string>())
            : _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    public Task<List<DepartmentExpansionPlan>> GetPlansAsync(int year, int? departmentId)
    {
        var query = _db.DepartmentExpansionPlans.AsNoTracking().Where(p => p.Year == year);
        if (departmentId.HasValue) query = query.Where(p => p.DepartmentId == departmentId.Value);
        return query.ToListAsync();
    }

    public Task<DepartmentExpansionPlan?> GetPlanAsync(int year, int departmentId) =>
        _db.DepartmentExpansionPlans.FirstOrDefaultAsync(p => p.Year == year && p.DepartmentId == departmentId);

    public async Task AddPlanAsync(DepartmentExpansionPlan plan) => await _db.DepartmentExpansionPlans.AddAsync(plan);

    public void RemovePlan(DepartmentExpansionPlan plan) => _db.DepartmentExpansionPlans.Remove(plan);

    public Task<List<AssetCategoryDeviceRatio>> GetAllRatiosAsync() =>
        _db.AssetCategoryDeviceRatios.AsNoTracking().ToListAsync();

    public Task<AssetCategoryDeviceRatio?> GetRatioAsync(int categoryId) =>
        _db.AssetCategoryDeviceRatios.FirstOrDefaultAsync(r => r.CategoryId == categoryId);

    public async Task AddRatioAsync(AssetCategoryDeviceRatio ratio) => await _db.AssetCategoryDeviceRatios.AddAsync(ratio);

    public void RemoveRatio(AssetCategoryDeviceRatio ratio) => _db.AssetCategoryDeviceRatios.Remove(ratio);
}
