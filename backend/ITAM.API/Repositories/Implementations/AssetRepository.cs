using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _db;

    public AssetRepository(AppDbContext db)
    {
        _db = db;
    }

    // AsNoTracking an toàn ở đây dù kết quả có thể bị sửa rồi lưu lại (UpdateAsync/DisposeAsync):
    // Update(asset) bên dưới luôn gọi _db.Assets.Update(asset) tường minh, tự attach + đánh dấu
    // Modified bất kể entity có đang được track hay không — không phụ thuộc change-tracking ngầm.
    public Task<Asset?> GetByIdWithDetailsAsync(int id) =>
        _db.Assets
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<Asset?> GetByAssetCodeAsync(string assetCode) =>
        _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetCode == assetCode);

    public Task<bool> SerialNumberExistsAsync(string serialNumber, int? excludeAssetId = null) =>
        _db.Assets.AnyAsync(a => a.SerialNumber == serialNumber && (excludeAssetId == null || a.Id != excludeAssetId));

    public async Task AddAsync(Asset asset) => await _db.Assets.AddAsync(asset);

    // Cùng lý do với UserRepository.Update: navigation Category/Department còn trỏ giá trị cũ sẽ ghi đè lại
    // CategoryId/DepartmentId vừa đổi. Chỉ dùng khoá ngoại và chỉ đánh dấu riêng tài sản này là Modified.
    public void Update(Asset asset)
    {
        asset.Category = null!;
        asset.Department = null!;
        var entry = _db.Entry(asset);
        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<bool> CategoryExistsAsync(int categoryId) =>
        _db.AssetCategories.AnyAsync(c => c.Id == categoryId);

    public Task<bool> DepartmentExistsAsync(int departmentId) =>
        _db.Departments.AnyAsync(d => d.Id == departmentId);

    // Phạm vi phòng ban; riêng Technician được mở rộng thêm các tài sản có phiếu Pending được giao cho mình.
    private static IQueryable<Asset> ApplyDepartmentScope(
        IQueryable<Asset> query, int? departmentId, int? orAssignedTechnicianId)
    {
        if (departmentId.HasValue && orAssignedTechnicianId.HasValue)
        {
            var technicianId = orAssignedTechnicianId.Value;
            var scopedDepartmentId = departmentId.Value;
            return query.Where(a => a.DepartmentId == scopedDepartmentId
                || a.MaintenanceTickets.Any(t => t.TechnicianId == technicianId && t.Status == TicketStatus.Pending));
        }

        return departmentId.HasValue ? query.Where(a => a.DepartmentId == departmentId.Value) : query;
    }

    public async Task<(List<Asset> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, AssetStatus? status, int? orAssignedTechnicianId, int page, int pageSize)
    {
        var query = _db.Assets.AsNoTracking().Include(a => a.Category).Include(a => a.Department).AsQueryable();

        query = ApplyDepartmentScope(query, departmentId, orAssignedTechnicianId);

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync();
        // Sắp xếp theo Mã tài sản (unique) để danh sách có thứ tự ổn định, dễ tra cứu.
        var items = await query
            .OrderBy(a => a.AssetCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Asset> Items, int TotalItems)> SearchAsync(
        string? keyword,
        int? departmentId,
        int? categoryId,
        AssetStatus? status,
        int? purchaseYear,
        bool? isUnderWarranty,
        int? orAssignedTechnicianId,
        int page,
        int pageSize)
    {
        var query = _db.Assets.AsNoTracking().Include(a => a.Category).Include(a => a.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(a => a.Name.Contains(keyword) || a.AssetCode.Contains(keyword));
        }

        query = ApplyDepartmentScope(query, departmentId, orAssignedTechnicianId);

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (purchaseYear.HasValue)
        {
            query = query.Where(a => a.PurchaseDate != null && a.PurchaseDate.Value.Year == purchaseYear.Value);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (isUnderWarranty == true)
        {
            query = query.Where(a => a.WarrantyExpiry != null && a.WarrantyExpiry.Value >= today);
        }
        else if (isUnderWarranty == false)
        {
            query = query.Where(a => a.WarrantyExpiry != null && a.WarrantyExpiry.Value < today);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(a => a.AssetCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
