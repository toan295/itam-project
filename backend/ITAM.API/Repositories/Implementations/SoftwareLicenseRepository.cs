using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class SoftwareLicenseRepository : ISoftwareLicenseRepository
{
    private readonly AppDbContext _context;

    public SoftwareLicenseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<SoftwareLicenseSnapshot> Items, int TotalItems)> GetPagedAsync(
        int page,
        int pageSize,
        SoftwareLicenseFilter filter)
    {
        var query = _context.SoftwareLicenses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var keyword = filter.Search.Trim();
            query = query.Where(license =>
                license.SoftwareName.Contains(keyword) ||
                license.LicenseKey.Contains(keyword) ||
                (license.Notes != null && license.Notes.Contains(keyword)) ||
                license.AssetSoftwareLicenses.Any(a => a.Asset.AssetCode.Contains(keyword) || a.Asset.Name.Contains(keyword)));
        }

        if (filter.ExpiryFrom.HasValue)
        {
            query = query.Where(license => license.ExpiryDate >= filter.ExpiryFrom.Value);
        }

        if (filter.ExpiryTo.HasValue)
        {
            query = query.Where(license => license.ExpiryDate <= filter.ExpiryTo.Value);
        }

        if (filter.State.HasValue)
        {
            var today = filter.Today;
            var warningDate = filter.WarningDate;
            query = filter.State.Value switch
            {
                LicenseStateFilter.Expired => query.Where(l => l.ExpiryDate < today),
                LicenseStateFilter.ExpiringSoon => query.Where(l => l.ExpiryDate >= today && l.ExpiryDate <= warningDate),
                LicenseStateFilter.Valid => query.Where(l => l.ExpiryDate > warningDate),
                // >= 80% nhưng chưa hết chỗ; dùng số nguyên (count*5 >= max*4) để khỏi phụ thuộc làm tròn số thực.
                LicenseStateFilter.NearLimit => query.Where(l =>
                    l.AssetSoftwareLicenses.Count() * 5 >= l.MaxUsage * 4 && l.AssetSoftwareLicenses.Count() < l.MaxUsage),
                _ => query,
            };
        }

        if (filter.Assignment.HasValue)
        {
            query = filter.Assignment.Value switch
            {
                LicenseAssignmentFilter.Unused => query.Where(l => !l.AssetSoftwareLicenses.Any()),
                LicenseAssignmentFilter.InUse => query.Where(l => l.AssetSoftwareLicenses.Any()),
                LicenseAssignmentFilter.Full => query.Where(l => l.AssetSoftwareLicenses.Count() >= l.MaxUsage),
                _ => query,
            };
        }

        var totalItems = await query.CountAsync();

        var ordered = filter.SortBy switch
        {
            LicenseSortBy.Newest => query.OrderByDescending(license => license.Id),
            LicenseSortBy.Expiry => query.OrderBy(license => license.ExpiryDate),
            LicenseSortBy.Usage => query.OrderByDescending(license =>
                (double)license.AssetSoftwareLicenses.Count() / license.MaxUsage),
            _ => query.OrderBy(license => license.SoftwareName),
        };

        var items = await ordered
            .ThenBy(license => license.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(license => new SoftwareLicenseSnapshot
            {
                Id = license.Id,
                SoftwareName = license.SoftwareName,
                LicenseKey = license.LicenseKey,
                ExpiryDate = license.ExpiryDate,
                MaxUsage = license.MaxUsage,
                Notes = license.Notes,
                // CurrentUsage is intentionally calculated from the join table.
                CurrentUsage = license.AssetSoftwareLicenses.Count()
            })
            .ToListAsync();

        return (items, totalItems);
    }

    public Task<SoftwareLicenseSnapshot?> GetByIdAsync(int id)
    {
        return _context.SoftwareLicenses
            .AsNoTracking()
            .Where(license => license.Id == id)
            .Select(license => new SoftwareLicenseSnapshot
            {
                Id = license.Id,
                SoftwareName = license.SoftwareName,
                LicenseKey = license.LicenseKey,
                ExpiryDate = license.ExpiryDate,
                MaxUsage = license.MaxUsage,
                Notes = license.Notes,
                CurrentUsage = license.AssetSoftwareLicenses.Count()
            })
            .SingleOrDefaultAsync();
    }

    public Task<SoftwareLicense?> GetEntityByIdAsync(int id)
    {
        return _context.SoftwareLicenses.SingleOrDefaultAsync(license => license.Id == id);
    }

    public async Task<IReadOnlyList<SoftwareLicenseSnapshot>> GetExpiringSoonAsync(
        DateOnly fromDate,
        DateOnly toDate)
    {
        return await _context.SoftwareLicenses
            .AsNoTracking()
            .Where(license => license.ExpiryDate >= fromDate && license.ExpiryDate <= toDate)
            .OrderBy(license => license.ExpiryDate)
            .Select(license => new SoftwareLicenseSnapshot
            {
                Id = license.Id,
                SoftwareName = license.SoftwareName,
                LicenseKey = license.LicenseKey,
                ExpiryDate = license.ExpiryDate,
                MaxUsage = license.MaxUsage,
                Notes = license.Notes,
                CurrentUsage = license.AssetSoftwareLicenses.Count()
            })
            .ToListAsync();
    }

    public Task<bool> LicenseKeyExistsAsync(string licenseKey, int? excludeId = null)
    {
        return _context.SoftwareLicenses.AnyAsync(license =>
            license.LicenseKey == licenseKey &&
            (!excludeId.HasValue || license.Id != excludeId.Value));
    }

    public Task<bool> AssetExistsAsync(int assetId)
    {
        return _context.Assets.AsNoTracking().AnyAsync(asset => asset.Id == assetId);
    }

    public Task<bool> IsAssetDisposedAsync(int assetId)
    {
        return _context.Assets.AsNoTracking().AnyAsync(asset =>
            asset.Id == assetId && asset.Status == ITAM.API.Models.Enums.AssetStatus.Disposed);
    }

    public async Task<IReadOnlyList<AssetSoftwareLicense>> GetAssignmentsAsync(int licenseId)
    {
        return await _context.AssetSoftwareLicenses.AsNoTracking()
            .Include(item => item.Asset).ThenInclude(asset => asset.Department)
            .Where(item => item.LicenseId == licenseId)
            .OrderBy(item => item.Asset.AssetCode)
            .ToListAsync();
    }

    public Task<bool> IsAssignedAsync(int licenseId, int assetId)
    {
        return _context.AssetSoftwareLicenses.AsNoTracking().AnyAsync(item =>
            item.LicenseId == licenseId && item.AssetId == assetId);
    }

    public Task<int> GetCurrentUsageAsync(int licenseId)
    {
        return _context.AssetSoftwareLicenses.AsNoTracking()
            .CountAsync(item => item.LicenseId == licenseId);
    }

    public async Task AddAsync(SoftwareLicense license)
    {
        _context.SoftwareLicenses.Add(license);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SoftwareLicense license)
    {
        _context.SoftwareLicenses.Update(license);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(SoftwareLicense license)
    {
        // Xoá mềm: giữ license trong CSDL. Các dòng "đang gán cho tài sản" bị gỡ (như khi xoá cứng trước đây) để không còn tài sản nào
        // hiển thị một license đã xoá.
        var assignments = await _context.AssetSoftwareLicenses.Where(a => a.LicenseId == license.Id).ToListAsync();
        _context.AssetSoftwareLicenses.RemoveRange(assignments);
        license.IsDeleted = true;
        license.DeletedAt = DateTime.UtcNow;
        _context.SoftwareLicenses.Update(license);
        await _context.SaveChangesAsync();
    }

    public async Task AssignAsync(AssetSoftwareLicense assignment)
    {
        _context.AssetSoftwareLicenses.Add(assignment);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> UnassignAsync(int licenseId, int assetId)
    {
        var assignment = await _context.AssetSoftwareLicenses.SingleOrDefaultAsync(item =>
            item.LicenseId == licenseId && item.AssetId == assetId);

        if (assignment is null)
        {
            return false;
        }

        _context.AssetSoftwareLicenses.Remove(assignment);
        await _context.SaveChangesAsync();
        return true;
    }
}
