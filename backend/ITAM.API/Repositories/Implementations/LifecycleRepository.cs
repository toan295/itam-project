using ITAM.API.Data;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class LifecycleRepository : ILifecycleRepository
{
    private readonly AppDbContext _context;

    public LifecycleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AssetLifecycleRow>> GetAssetLifecycleRowsAsync(int? departmentId)
    {
        // D3: tài sản đã thanh lý (Disposed) không tham gia phân tích vòng đời — loại ngay ở truy vấn để
        // mọi thống kê, danh sách đề xuất và dự báo ngân sách đều không bao giờ thấy chúng.
        // Projection bên dưới tự JOIN Category/Department nên không cần Include.
        var assetQuery = _context.Assets
            .AsNoTracking()
            .Where(asset => asset.Status != AssetStatus.Disposed);

        if (departmentId.HasValue)
        {
            assetQuery = assetQuery.Where(asset => asset.DepartmentId == departmentId.Value);
        }

        var rows = await assetQuery
            .OrderBy(asset => asset.Id)
            .Select(asset => new AssetLifecycleRow
            {
                AssetId = asset.Id,
                AssetCode = asset.AssetCode,
                AssetName = asset.Name,
                CategoryId = asset.CategoryId,
                CategoryName = asset.Category.Name,
                DepartmentId = asset.DepartmentId,
                DepartmentName = asset.Department.Name,
                PurchaseDate = asset.PurchaseDate,
                TicketCount = 0,
                FailedTicketCount = 0,
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return rows;
        }

        // Truy vấn tổng hợp riêng, sau đó ghép trong bộ nhớ: luôn tối đa 2 truy vấn,
        // không phát sinh N+1 khi số lượng tài sản tăng.
        // EXISTS theo cùng điều kiện lọc tài sản thay vì IN (danh sách Id): không phình số tham số khi có hàng nghìn tài sản.
        var ticketStats = await _context.MaintenanceTickets
            .AsNoTracking()
            .Where(ticket => assetQuery.Any(asset => asset.Id == ticket.AssetId))
            .GroupBy(ticket => ticket.AssetId)
            .Select(group => new
            {
                AssetId = group.Key,
                Total = group.Count(),
                Failed = group.Count(ticket => ticket.Status == TicketStatus.Failed),
            })
            .ToDictionaryAsync(item => item.AssetId);

        foreach (var row in rows)
        {
            if (!ticketStats.TryGetValue(row.AssetId, out var stats))
            {
                continue;
            }

            row.TicketCount = stats.Total;
            row.FailedTicketCount = stats.Failed;
        }

        return rows;
    }

    public async Task<IReadOnlyList<AssetCategoryLifecyclePolicy>> GetPoliciesAsync() =>
        await _context.AssetCategoryLifecyclePolicies
            .AsNoTracking()
            .OrderBy(policy => policy.CategoryId)
            .ToListAsync();

    public async Task<IReadOnlyList<AssetCategory>> GetCategoriesAsync() =>
        await _context.AssetCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync();

    public Task<AssetCategory?> GetCategoryByIdAsync(int categoryId) =>
        _context.AssetCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Id == categoryId);

    public Task<AssetCategoryLifecyclePolicy?> GetPolicyByCategoryIdAsync(int categoryId) =>
        _context.AssetCategoryLifecyclePolicies
            .FirstOrDefaultAsync(policy => policy.CategoryId == categoryId);

    public async Task AddPolicyAsync(AssetCategoryLifecyclePolicy policy) =>
        await _context.AssetCategoryLifecyclePolicies.AddAsync(policy);

    public void RemovePolicy(AssetCategoryLifecyclePolicy policy) =>
        _context.AssetCategoryLifecyclePolicies.Remove(policy);

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
