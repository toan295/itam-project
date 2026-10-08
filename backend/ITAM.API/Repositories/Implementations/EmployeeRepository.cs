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
        _db.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<(List<Employee> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, string? keyword, bool? isActive, int page, int pageSize)
    {
        var query = _db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(e => e.FullName.Contains(k) || e.EmployeeCode.Contains(k)
                || (e.Position != null && e.Position.Contains(k)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(e => e.FullName).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<bool> NameExistsInDepartmentAsync(string fullName, int departmentId, int? excludeId = null) =>
        _db.Employees.AnyAsync(e =>
            e.DepartmentId == departmentId && e.FullName == fullName && (excludeId == null || e.Id != excludeId));

    public Task<bool> DepartmentExistsAsync(int departmentId) =>
        _db.Departments.AnyAsync(d => d.Id == departmentId);

    public Task<bool> HasOpenAllocationAsync(int employeeId) =>
        _db.AssetAllocations.AnyAsync(a =>
            a.EmployeeId == employeeId && a.Status == AllocationStatus.Allocated && a.ReturnedDate == null);

    public Task<bool> HasAnyAllocationAsync(int employeeId) =>
        _db.AssetAllocations.AnyAsync(a => a.EmployeeId == employeeId);

    public async Task AddAsync(Employee employee) => await _db.Employees.AddAsync(employee);

    public void Remove(Employee employee) => _db.Employees.Remove(employee);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
