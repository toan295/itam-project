using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _db;

    public EmployeeRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Employee?> GetByIdAsync(int id) =>
        _db.Employees.Include(e => e.Department).Include(e => e.User).ThenInclude(u => u!.Role).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<(List<Employee> Items, int TotalItems)> GetPagedAsync(EmployeeListFilter f, int page, int pageSize)
    {
        var query = _db.Employees.AsNoTracking().Include(e => e.Department).Include(e => e.User).ThenInclude(u => u!.Role).AsQueryable();

        if (f.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == f.DepartmentId.Value);
        }

        if (f.ExcludeStaffAccounts)
        {
            query = query.Where(e => e.UserId == null || e.User!.Role.Name == "Employee");
        }

        if (f.IsActive.HasValue)
        {
            query = query.Where(e => e.IsActive == f.IsActive.Value);
        }

        // Thứ tự ưu tiên khớp huy hiệu ở cột "Tài khoản": chờ kích hoạt > đã khoá > đang dùng.
        switch (f.AccountState)
        {
            case "none":
                query = query.Where(e => e.UserId == null);
                break;
            case "pending":
                query = query.Where(e => e.UserId != null && e.User!.PendingActivation);
                break;
            case "locked":
                query = query.Where(e => e.UserId != null && !e.User!.PendingActivation && !e.User.IsActive);
                break;
            case "active":
                query = query.Where(e => e.UserId != null && !e.User!.PendingActivation && e.User.IsActive);
                break;
        }

        if (!string.IsNullOrWhiteSpace(f.RoleName))
        {
            query = query.Where(e => e.UserId != null && e.User!.Role.Name == f.RoleName);
        }

        if (f.HoldingAssets.HasValue)
        {
            var holding = f.HoldingAssets.Value;
            query = query.Where(e => _db.AssetAllocations.Any(a =>
                a.EmployeeId == e.Id && a.Status == AllocationStatus.Allocated && a.ReturnedDate == null) == holding);
        }

        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim();
            // Số điện thoại lưu dạng chuẩn (không khoảng trắng/dấu): bỏ ký tự phân cách trong từ khoá rồi mới so khớp.
            var phoneKey = System.Text.RegularExpressions.Regex.Replace(k, @"[\s.\-()]", "");
            var phoneSearch = phoneKey.Length > 0 && phoneKey.All(c => char.IsDigit(c) || c == '+');
            query = query.Where(e => e.FullName.Contains(k) || e.EmployeeCode.Contains(k)
                || (e.Position != null && e.Position.Contains(k))
                || (e.Email != null && e.Email.Contains(k))
                || (phoneSearch && e.Phone != null && e.Phone.Contains(phoneKey)));
        }

        var total = await query.CountAsync();
        var ordered = f.Sort switch
        {
            "code" => query.OrderBy(e => e.EmployeeCode),
            "department" => query.OrderBy(e => e.Department.Name).ThenBy(e => e.FullName),
            "newest" => query.OrderByDescending(e => e.Id),
            _ => query.OrderBy(e => e.FullName),
        };
        var items = await ordered
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<List<string>> GetPositionSuggestionsAsync(int? departmentId, bool excludeStaffAccounts, int take)
    {
        var query = _db.Employees.AsNoTracking().Where(e => e.Position != null && e.Position != "");
        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (excludeStaffAccounts)
        {
            query = query.Where(e => e.UserId == null || e.User!.Role.Name == "Employee");
        }

        return query.GroupBy(e => e.Position!)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key)
            .Take(take)
            .ToListAsync();
    }

    public Task<List<Employee>> GetAccountCandidatesAsync(string? keyword, int take)
    {
        var query = _db.Employees.AsNoTracking().Include(e => e.Department)
            .Where(e => e.IsActive && e.UserId == null);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(e => e.FullName.Contains(k) || e.EmployeeCode.Contains(k)
                || (e.Email != null && e.Email.Contains(k)));
        }

        return query.OrderBy(e => e.FullName).ThenBy(e => e.Id).Take(take).ToListAsync();
    }

    public Task<bool> NameExistsInDepartmentAsync(string fullName, int departmentId, int? excludeId = null) =>
        _db.Employees.AnyAsync(e =>
            e.DepartmentId == departmentId && e.FullName == fullName && (excludeId == null || e.Id != excludeId));

    public Task<bool> EmailExistsAsync(string email, int? excludeId = null) =>
        _db.Employees.AnyAsync(e => e.Email == email && (excludeId == null || e.Id != excludeId));

    public Task<bool> DepartmentExistsAsync(int departmentId) =>
        _db.Departments.AnyAsync(d => d.Id == departmentId);

    public Task<bool> HasOpenAllocationAsync(int employeeId) =>
        _db.AssetAllocations.AnyAsync(a =>
            a.EmployeeId == employeeId && a.Status == AllocationStatus.Allocated && a.ReturnedDate == null);

    public Task<bool> HasAnyAllocationAsync(int employeeId) =>
        _db.AssetAllocations.AnyAsync(a => a.EmployeeId == employeeId);

    public Task<Employee?> GetByUserIdAsync(int userId) =>
        _db.Employees.AsNoTracking().Include(e => e.Department).FirstOrDefaultAsync(e => e.UserId == userId);

    public Task<Employee?> GetByUserIdTrackedAsync(int userId) =>
        _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);

    public Task<bool> HasOpenAllocationForAssetAsync(int employeeId, int assetId) =>
        _db.AssetAllocations.AnyAsync(a =>
            a.EmployeeId == employeeId && a.AssetId == assetId
            && a.Status == AllocationStatus.Allocated && a.ReturnedDate == null);

    public Task<List<AssetAllocation>> GetOpenAllocationsWithAssetAsync(int employeeId) =>
        _db.AssetAllocations.AsNoTracking()
            .Include(a => a.Asset).ThenInclude(x => x.Category)
            .Where(a => a.EmployeeId == employeeId && a.Status == AllocationStatus.Allocated && a.ReturnedDate == null)
            .OrderBy(a => a.Asset.AssetCode)
            .ToListAsync();

    public async Task<HashSet<int>> GetAssetIdsWithPendingTicketsAsync(IEnumerable<int> assetIds)
    {
        var ids = assetIds.Distinct().ToList();
        var pending = await _db.MaintenanceTickets.AsNoTracking()
            .Where(t => ids.Contains(t.AssetId) && t.Status == TicketStatus.Pending)
            .Select(t => t.AssetId)
            .Distinct()
            .ToListAsync();
        return pending.ToHashSet();
    }

    public async Task AddAsync(Employee employee) => await _db.Employees.AddAsync(employee);

    // Xoá mềm: giữ dòng trong CSDL, chỉ đánh dấu đã xoá.
    public void Remove(Employee employee)
    {
        employee.IsDeleted = true;
        employee.DeletedAt = DateTime.UtcNow;
        _db.Employees.Update(employee);
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
