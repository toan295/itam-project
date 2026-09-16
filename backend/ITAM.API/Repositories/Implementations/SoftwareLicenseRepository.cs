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
        string? search = null)
    {
        var query = _context.SoftwareLicenses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(license =>
                license.SoftwareName.Contains(keyword) ||
                license.LicenseKey.Contains(keyword));
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(license => license.SoftwareName)
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
        _context.SoftwareLicenses.Remove(license);
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
