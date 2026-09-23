using ITAM.API.Data;
using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AssetAllocationRepository : IAssetAllocationRepository
{
    private readonly AppDbContext _context;

    public AssetAllocationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AssetAllocation?> GetByIdWithDetailsAsync(int id) =>
        _context.AssetAllocations
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<AssetAllocation?> GetEntityByIdAsync(int id) =>
        _context.AssetAllocations
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<bool> HasOpenAllocationAsync(int assetId) =>
        _context.AssetAllocations.AnyAsync(x =>
            x.AssetId == assetId
            && x.Status == AllocationStatus.Allocated
            && x.ReturnedDate == null);

    public Task<AssetAllocation?> GetOpenAllocationByAssetIdAsync(int assetId) =>
        _context.AssetAllocations
            .FirstOrDefaultAsync(x =>
                x.AssetId == assetId
                && x.Status == AllocationStatus.Allocated
                && x.ReturnedDate == null);

    public async Task<(List<AssetAllocation> Items, int TotalItems)> GetPagedAsync(
        int? departmentId,
        AllocationStatus? status,
        int page,
        int pageSize)
    {
        var query = _context.AssetAllocations
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.Department)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(x => x.DepartmentId == departmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.AllocatedDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }

    public async Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        DateOnly today,
        int thresholdDays,
        int? departmentId)
    {
        var allocationQuery = _context.AssetAllocations
            .AsNoTracking()
            .Include(x => x.Asset)
            .Include(x => x.Department)
            .Where(x => x.Status == AllocationStatus.Allocated && x.ReturnedDate == null);

        if (departmentId.HasValue)
        {
            allocationQuery = allocationQuery.Where(x => x.DepartmentId == departmentId.Value);
        }

        var allocations = await allocationQuery.ToListAsync();
        if (allocations.Count == 0)
        {
            return Array.Empty<OverdueAllocationDto>();
        }

        // Tách truy vấn MAX bảo trì thành bước riêng để LINQ dịch ổn định sang MySQL,
        // đúng hướng dẫn của tài liệu Tuần 5 khi LEFT JOIN/MAX trở nên phức tạp.
        var assetIds = allocations.Select(x => x.AssetId).Distinct().ToList();
        var maintenanceDates = await _context.MaintenanceTickets
            .AsNoTracking()
            .Where(ticket => assetIds.Contains(ticket.AssetId))
            .GroupBy(ticket => ticket.AssetId)
            .Select(group => new
            {
                AssetId = group.Key,
                LastMaintenanceDate = group.Max(ticket => (DateTime?)ticket.ReportedDate),
            })
            .ToDictionaryAsync(x => x.AssetId, x => x.LastMaintenanceDate);

        var result = new List<OverdueAllocationDto>();
        foreach (var allocation in allocations)
        {
            maintenanceDates.TryGetValue(allocation.AssetId, out var lastMaintenanceDateTime);
            var lastMaintenanceDate = lastMaintenanceDateTime.HasValue
                ? DateOnly.FromDateTime(lastMaintenanceDateTime.Value)
                : (DateOnly?)null;

            var comparisonDate = lastMaintenanceDate
                ?? allocation.Asset.PurchaseDate
                ?? allocation.AllocatedDate;

            var daysSinceLastMaintenance = today.DayNumber - comparisonDate.DayNumber;
            if (daysSinceLastMaintenance <= thresholdDays)
            {
                continue;
            }

            result.Add(new OverdueAllocationDto
            {
                AllocationId = allocation.Id,
                AssetId = allocation.AssetId,
                AssetCode = allocation.Asset.AssetCode,
                AssetName = allocation.Asset.Name,
                DepartmentName = allocation.Department.Name,
                RecipientName = allocation.RecipientName,
                LastMaintenanceDate = lastMaintenanceDate,
                DaysSinceLastMaintenance = daysSinceLastMaintenance,
            });
        }

        return result
            .OrderByDescending(x => x.DaysSinceLastMaintenance)
            .ToList();
    }

    public async Task AddAsync(AssetAllocation allocation) =>
        await _context.AssetAllocations.AddAsync(allocation);

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
