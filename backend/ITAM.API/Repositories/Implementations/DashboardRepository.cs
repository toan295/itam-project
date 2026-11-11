using ITAM.API.Data;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<ITAM.API.Models.Entities.Asset> Assets(int? departmentId, bool includeDisposed = false)
    {
        var query = _db.Assets.AsNoTracking().AsQueryable();
        if (!includeDisposed)
        {
            query = query.Where(a => a.Status != AssetStatus.Disposed);
        }

        return departmentId.HasValue ? query.Where(a => a.DepartmentId == departmentId.Value) : query;
    }

    public async Task<Dictionary<AssetStatus, int>> GetAssetStatusCountsAsync(int? departmentId) =>
        await Assets(departmentId, includeDisposed: true)
            .GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task<List<AssetGroupCount>> GetCategoryStatusCountsAsync(int? departmentId) =>
        Assets(departmentId)
            .GroupBy(a => new { a.CategoryId, a.Category.Name, a.Status })
            .Select(g => new AssetGroupCount(g.Key.CategoryId, g.Key.Name, g.Key.Status, g.Count()))
            .ToListAsync();

    public Task<List<AssetGroupCount>> GetDepartmentStatusCountsAsync(int? departmentId) =>
        Assets(departmentId)
            .GroupBy(a => new { a.DepartmentId, a.Department.Name, a.Status })
            .Select(g => new AssetGroupCount(g.Key.DepartmentId, g.Key.Name, g.Key.Status, g.Count()))
            .ToListAsync();

    // Năm mua gom ở DB; ngày mua trống hoặc ở tương lai (dữ liệu sai, như module Vòng đời) gom vào Year = 0.
    // Hai truy vấn gom nhóm cố định (EF không dịch được truy vấn con tương quan nằm trong GROUP BY), ghép trong bộ nhớ.
    public async Task<List<PurchaseYearCount>> GetPurchaseYearCountsAsync(int? departmentId, DateOnly today)
    {
        var totals = await Assets(departmentId)
            .GroupBy(a => a.PurchaseDate == null || a.PurchaseDate > today ? 0 : a.PurchaseDate.Value.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToListAsync();

        var withTicket = await Assets(departmentId)
            .Where(a => _db.MaintenanceTickets.Any(t => t.AssetId == a.Id))
            .GroupBy(a => a.PurchaseDate == null || a.PurchaseDate > today ? 0 : a.PurchaseDate.Value.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Year, x => x.Count);

        return totals
            .OrderBy(x => x.Year)
            .Select(x => new PurchaseYearCount(x.Year, x.Count, withTicket.GetValueOrDefault(x.Year)))
            .ToList();
    }

    public async Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(int? departmentId)
    {
        var query = _db.MaintenanceTickets.AsNoTracking().Where(t => t.Asset.Status != AssetStatus.Disposed);
        if (departmentId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        }

        return await query.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    public async Task<LicenseCounts> GetLicenseCountsAsync(DateOnly today, int expiringDays)
    {
        var limit = today.AddDays(expiringDays);
        var row = await _db.SoftwareLicenses.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Expired = g.Count(l => l.ExpiryDate < today),
                Soon = g.Count(l => l.ExpiryDate >= today && l.ExpiryDate <= limit),
            })
            .FirstOrDefaultAsync();
        return row is null ? new LicenseCounts(0, 0, 0) : new LicenseCounts(row.Total, row.Soon, row.Expired);
    }

    public async Task<BudgetTotals> GetBudgetTotalsAsync(int year, int? departmentId)
    {
        var query = _db.BudgetForecasts.AsNoTracking().Where(f => f.Year == year);   // query filter đã loại dòng xoá mềm
        if (departmentId.HasValue)
        {
            query = query.Where(f => f.DepartmentId == departmentId.Value);
        }

        var row = await query.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Replacement = g.Sum(f => f.EstimatedReplacementCount),
            Expansion = g.Sum(f => f.EstimatedExpansionCount),
            ReplacementBudget = g.Sum(f => f.EstimatedBudget),
            ExpansionBudget = g.Sum(f => f.EstimatedExpansionBudget),
            Latest = g.Max(f => f.GeneratedAt),
        }).FirstOrDefaultAsync();

        return row is null
            ? new BudgetTotals(0, 0, 0, 0m, 0m, null)
            : new BudgetTotals(row.Count, row.Replacement, row.Expansion, row.ReplacementBudget, row.ExpansionBudget, row.Latest);
    }

    public Task<string?> GetDepartmentNameAsync(int departmentId) =>
        _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => (string?)d.Name).FirstOrDefaultAsync();
}
