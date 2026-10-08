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
        int? departmentId, int? assetId, string? statusCode, int? technicianUserId, int page, int pageSize)
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

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
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

    public async Task AddAsync(DisposalRequest request) => await _db.DisposalRequests.AddAsync(request);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
