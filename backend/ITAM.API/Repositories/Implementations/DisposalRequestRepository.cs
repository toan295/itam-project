using ITAM.API.Repositories.Models;
using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class DisposalRequestRepository : IDisposalRequestRepository
{
    private readonly AppDbContext _db;

    public DisposalRequestRepository(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<DisposalRequest> WithDetails() =>
        _db.DisposalRequests
            .Include(r => r.Asset).ThenInclude(a => a.Department)
            .Include(r => r.Status)
            .Include(r => r.SubStatus)
            .Include(r => r.InspectedBy)
            .Include(r => r.ProposedBy)
            .Include(r => r.ReviewedBy)
            .Include(r => r.CompletedBy);

    public Task<DisposalRequest?> GetByIdWithDetailsAsync(int id) =>
        WithDetails().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<(List<DisposalRequest> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, string? statusCode, int? technicianUserId, int page, int pageSize,
        DisposalListExtras extras)
    {
        var query = WithDetails().AsNoTracking();

        if (departmentId.HasValue)
        {
            query = query.Where(r => r.Asset.DepartmentId == departmentId.Value);
        }

        if (assetId.HasValue)
        {
            query = query.Where(r => r.AssetId == assetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            query = query.Where(r => r.Status.Code == statusCode);
        }

        // Technician chỉ thấy phiếu do chính mình kiểm tra/đề xuất.
        if (technicianUserId.HasValue)
        {
            var me = technicianUserId.Value;
            query = query.Where(r => r.InspectedByUserId == me || r.ProposedByUserId == me);
        }

        if (!string.IsNullOrWhiteSpace(extras.Keyword))
        {
            var k = extras.Keyword.Trim();
            query = query.Where(r => r.Asset.AssetCode.Contains(k) || r.Asset.Name.Contains(k)
                || (r.Reason != null && r.Reason.Contains(k))
                || (r.DisposalMethod != null && r.DisposalMethod.Contains(k))
                || r.InspectedBy.FullName.Contains(k)
                || (r.ProposedBy != null && r.ProposedBy.FullName.Contains(k)));
        }

        if (extras.CategoryId.HasValue)
        {
            query = query.Where(r => r.Asset.CategoryId == extras.CategoryId.Value);
        }

        if (extras.CreatedFrom.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= extras.CreatedFrom.Value);
        }

        if (extras.CreatedTo.HasValue)
        {
            query = query.Where(r => r.CreatedAt < extras.CreatedTo.Value);
        }

        var total = await query.CountAsync();
        var ordered = extras.Sort == "oldest"
            ? query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Asset> Items, int TotalItems)> GetDisposalCandidatesAsync(int? departmentId, int page, int pageSize)
    {
        var query = _db.Assets.AsNoTracking()
            .Include(a => a.Category).Include(a => a.Department)
            .Where(a => a.Status == AssetStatus.Broken
                && !_db.DisposalRequests.Any(r => r.AssetId == a.Id
                    && r.Status.Code != DisposalStatusCodes.Rejected && r.Status.Code != DisposalStatusCodes.Completed));

        if (departmentId.HasValue)
        {
            query = query.Where(a => a.DepartmentId == departmentId.Value);
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(a => a.AssetCode).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public Task<bool> HasOpenRequestForAssetAsync(int assetId) =>
        _db.DisposalRequests.AnyAsync(r =>
            r.AssetId == assetId
            && r.Status.Code != DisposalStatusCodes.Rejected
            && r.Status.Code != DisposalStatusCodes.Completed);

    public async Task<int> ReleaseSoftwareLicensesAsync(int assetId)
    {
        var assignments = await _db.AssetSoftwareLicenses.Where(a => a.AssetId == assetId).ToListAsync();
        _db.AssetSoftwareLicenses.RemoveRange(assignments);
        return assignments.Count;
    }

    public async Task AddAsync(DisposalRequest request) => await _db.DisposalRequests.AddAsync(request);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
