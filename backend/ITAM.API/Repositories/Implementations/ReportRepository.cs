using ITAM.API.Data;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;

    public ReportRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<InventoryRow>> GetInventoryAsync(InventoryFilter filter, int limit)
    {
        var query = _db.Assets.AsNoTracking().AsQueryable();
        if (filter.DepartmentId.HasValue) query = query.Where(a => a.DepartmentId == filter.DepartmentId.Value);
        if (filter.CategoryId.HasValue) query = query.Where(a => a.CategoryId == filter.CategoryId.Value);
        if (filter.Status.HasValue) query = query.Where(a => a.Status == filter.Status.Value);
        if (filter.PurchaseYear.HasValue) query = query.Where(a => a.PurchaseDate != null && a.PurchaseDate.Value.Year == filter.PurchaseYear.Value);
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var k = filter.Keyword.Trim();
            query = query.Where(a => a.Name.Contains(k) || a.AssetCode.Contains(k) || (a.SerialNumber != null && a.SerialNumber.Contains(k)));
        }

        return query
            .OrderBy(a => a.AssetCode)
            .Take(limit + 1)
            .Select(a => new InventoryRow(
                a.AssetCode, a.Name, a.Category.Name, a.Department.Name, a.SerialNumber, a.Status, a.PurchaseDate, a.WarrantyExpiry,
                _db.AssetAllocations.Where(al => al.AssetId == a.Id && al.Status == AllocationStatus.Allocated && al.ReturnedDate == null)
                    .OrderByDescending(al => al.AllocatedDate).Select(al => al.RecipientName).FirstOrDefault()))
            .ToListAsync();
    }

    // Phiếu đang xử lý (Pending) của tài sản chưa thanh lý, cũ nhất trước (xử lý lâu nhất lên đầu).
    public Task<List<OpenTicketRow>> GetOpenTicketsAsync(int? departmentId, int? categoryId, int limit)
    {
        var query = _db.MaintenanceTickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Pending && t.Asset.Status != AssetStatus.Disposed);
        if (departmentId.HasValue) query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        if (categoryId.HasValue) query = query.Where(t => t.Asset.CategoryId == categoryId.Value);

        return query
            .OrderBy(t => t.ReportedDate).ThenBy(t => t.Id)
            .Take(limit + 1)
            .Select(t => new OpenTicketRow(
                t.Id, t.Asset.AssetCode, t.Asset.Name, t.Asset.Category.Name, t.Asset.Department.Name, t.Priority,
                t.ReportedDate, t.Technician != null ? t.Technician.FullName : null, t.IssueDescription))
            .ToListAsync();
    }

    // Bảo hành còn hiệu lực và hết trong khoảng [from, to] (gồm hai đầu), tài sản chưa thanh lý; sắp hết sớm nhất lên đầu.
    public Task<List<WarrantyRow>> GetWarrantyExpiringAsync(int? departmentId, int? categoryId, DateOnly from, DateOnly to, int limit)
    {
        var query = _db.Assets.AsNoTracking()
            .Where(a => a.Status != AssetStatus.Disposed && a.WarrantyExpiry != null && a.WarrantyExpiry >= from && a.WarrantyExpiry <= to);
        if (departmentId.HasValue) query = query.Where(a => a.DepartmentId == departmentId.Value);
        if (categoryId.HasValue) query = query.Where(a => a.CategoryId == categoryId.Value);

        return query
            .OrderBy(a => a.WarrantyExpiry).ThenBy(a => a.AssetCode)
            .Take(limit + 1)
            .Select(a => new WarrantyRow(a.AssetCode, a.Name, a.Category.Name, a.Department.Name, a.Status, a.PurchaseDate, a.WarrantyExpiry!.Value))
            .ToListAsync();
    }

    // License sắp hết hạn sớm nhất lên đầu (đã hết hạn nằm trước), cùng ngày thì theo tên phần mềm.
    public Task<List<LicenseRow>> GetLicensesAsync(int limit) =>
        _db.SoftwareLicenses.AsNoTracking()
            .OrderBy(l => l.ExpiryDate).ThenBy(l => l.SoftwareName).ThenBy(l => l.Id)
            .Take(limit + 1)
            .Select(l => new LicenseRow(l.Id, l.SoftwareName, l.LicenseKey, l.ExpiryDate, l.MaxUsage, l.AssetSoftwareLicenses.Count(), l.Notes))
            .ToListAsync();

    public Task<List<LicenseAssignmentRow>> GetLicenseAssignmentsAsync(int limit) =>
        _db.AssetSoftwareLicenses.AsNoTracking()
            .OrderBy(a => a.License.SoftwareName).ThenBy(a => a.Asset.AssetCode)
            .Take(limit + 1)
            .Select(a => new LicenseAssignmentRow(a.LicenseId, a.License.SoftwareName, a.Asset.AssetCode, a.Asset.Name, a.Asset.Department.Name, a.AssignedDate))
            .ToListAsync();

    public Task<string?> GetUserNameAsync(int userId) =>
        _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (string?)u.FullName).FirstOrDefaultAsync();

    public Task<string?> GetDepartmentNameAsync(int departmentId) =>
        _db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => (string?)d.Name).FirstOrDefaultAsync();

    public Task<string?> GetCategoryNameAsync(int categoryId) =>
        _db.AssetCategories.AsNoTracking().Where(c => c.Id == categoryId).Select(c => (string?)c.Name).FirstOrDefaultAsync();
}
