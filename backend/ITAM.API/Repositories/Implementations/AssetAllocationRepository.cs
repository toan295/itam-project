using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
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
            .Include(x => x.Asset).ThenInclude(a => a.Department)
            .Include(x => x.Department)
            .Include(x => x.Employee).ThenInclude(e => e!.Department)
            .Include(x => x.HandedOverBy).ThenInclude(u => u!.Role)
            .Include(x => x.HandedOverBy).ThenInclude(u => u!.Department)
            .Include(x => x.ReceivedBy).ThenInclude(u => u!.Role)
            .Include(x => x.ReceivedBy).ThenInclude(u => u!.Department)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<decimal?> GetReferencePriceAsync(int categoryId) =>
        await _context.AssetCategoryReferencePrices
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .Select(p => (decimal?)p.UnitPrice)
            .FirstOrDefaultAsync();

    // Tracked (không AsNoTracking) + Include(Asset) — xem giải thích trong interface.
    public Task<AssetAllocation?> GetEntityByIdAsync(int id) =>
        _context.AssetAllocations
            .Include(x => x.Asset)
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
        string? keyword,
        int? employeeId,
        int page,
        int pageSize,
        AllocationListExtras extras)
    {
        var query = _context.AssetAllocations
            .AsNoTracking()
            .Include(x => x.Asset).ThenInclude(a => a.Department)
            .Include(x => x.Department)
            .Include(x => x.Employee).ThenInclude(e => e!.Department)
            .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                x.RecipientName.Contains(k)
                || (x.Employee != null && x.Employee.EmployeeCode.Contains(k))
                || x.Asset.AssetCode.Contains(k)
                || x.Asset.Name.Contains(k));
        }

        if (departmentId.HasValue)
        {
            var dept = departmentId.Value;
            query = extras.DepartmentScope switch
            {
                "employee" => query.Where(x => x.Employee != null && x.Employee.DepartmentId == dept),
                "asset" => query.Where(x => x.Asset.DepartmentId == dept),
                _ => query.Where(x => x.DepartmentId == dept),
            };
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (extras.CategoryId.HasValue)
        {
            query = query.Where(x => x.Asset.CategoryId == extras.CategoryId.Value);
        }

        if (extras.AllocatedFrom.HasValue)
        {
            query = query.Where(x => x.AllocatedDate >= extras.AllocatedFrom.Value);
        }

        if (extras.AllocatedTo.HasValue)
        {
            query = query.Where(x => x.AllocatedDate <= extras.AllocatedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(extras.HandoverCondition))
        {
            query = query.Where(x => x.HandoverCondition == extras.HandoverCondition);
        }

        if (extras.ReturnCondition.HasValue)
        {
            query = query.Where(x => x.ReturnCondition == extras.ReturnCondition.Value);
        }

        if (extras.HoldingBefore.HasValue)
        {
            query = query.Where(x => x.Status == AllocationStatus.Allocated && x.ReturnedDate == null
                && x.AllocatedDate < extras.HoldingBefore.Value);
        }

        var totalItems = await query.CountAsync();
        var ordered = extras.Sort == "oldest"
            ? query.OrderBy(x => x.AllocatedDate).ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.AllocatedDate).ThenByDescending(x => x.Id);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }

    // Chỉ đọc dữ liệu thô (allocation đang mở + ngày bảo trì gần nhất) — quyết định "quá hạn hay chưa"
    // (so sánh với thresholdDays) thuộc về Service, đúng quy ước "Repository không chứa nghiệp vụ".
    public async Task<List<AllocationOverdueCandidateRow>> GetOpenAllocationCandidatesAsync(int? departmentId)
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
            return new List<AllocationOverdueCandidateRow>();
        }

        // Tách truy vấn MAX bảo trì thành bước riêng để LINQ dịch ổn định sang MySQL,
        // đúng hướng dẫn của tài liệu Tuần 5 khi LEFT JOIN/MAX trở nên phức tạp.
        var assetIds = allocations.Select(x => x.AssetId).Distinct().ToList();
        // Mốc bảo trì = lần HOÀN TẤT (Resolved) gần nhất; phiếu chờ xử lý/không xử lý được không tính là đã bảo trì.
        var maintenanceDates = await _context.MaintenanceTickets
            .AsNoTracking()
            .Where(ticket => assetIds.Contains(ticket.AssetId) && ticket.Status == TicketStatus.Resolved)
            .GroupBy(ticket => ticket.AssetId)
            .Select(group => new
            {
                AssetId = group.Key,
                LastMaintenanceDate = group.Max(ticket => (DateTime?)(ticket.ResolvedDate ?? ticket.ReportedDate)),
            })
            .ToDictionaryAsync(x => x.AssetId, x => x.LastMaintenanceDate);
        var pendingAssetIds = (await _context.MaintenanceTickets
            .AsNoTracking()
            .Where(ticket => assetIds.Contains(ticket.AssetId) && ticket.Status == TicketStatus.Pending)
            .Select(ticket => ticket.AssetId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        return allocations.Select(allocation =>
        {
            maintenanceDates.TryGetValue(allocation.AssetId, out var lastMaintenanceDate);
            return new AllocationOverdueCandidateRow
            {
                AllocationId = allocation.Id,
                AssetId = allocation.AssetId,
                AssetCode = allocation.Asset.AssetCode,
                AssetName = allocation.Asset.Name,
                DepartmentName = allocation.Department.Name,
                RecipientName = allocation.RecipientName,
                AllocatedDate = allocation.AllocatedDate,
                AssetPurchaseDate = allocation.Asset.PurchaseDate,
                LastMaintenanceDate = lastMaintenanceDate,
                HasPendingTicket = pendingAssetIds.Contains(allocation.AssetId),
            };
        }).ToList();
    }

    public async Task AddAsync(AssetAllocation allocation) =>
        await _context.AssetAllocations.AddAsync(allocation);

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
