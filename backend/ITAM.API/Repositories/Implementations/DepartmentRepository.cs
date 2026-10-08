using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _db;

    public DepartmentRepository(AppDbContext db)
    {
        _db = db;
    }

    // Sắp xếp theo Id để danh sách (và các dropdown dùng chung) có thứ tự ổn định, dễ đối chiếu.
    public Task<List<Department>> GetAllAsync() =>
        _db.Departments.AsNoTracking().OrderBy(d => d.Id).ToListAsync();

    // AsNoTracking an toàn dù kết quả có thể bị sửa/xoá rồi lưu lại — Update()/Remove() bên dưới luôn
    // gọi tường minh, không phụ thuộc change-tracking ngầm (cùng cách với AssetCategoryRepository).
    public Task<Department?> GetByIdAsync(int id) =>
        _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

    public Task<Department?> GetByNameAsync(string name) =>
        _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Name == name);

    public async Task AddAsync(Department department) => await _db.Departments.AddAsync(department);

    public void Update(Department department) => _db.Departments.Update(department);

    public void Remove(Department department) => _db.Departments.Remove(department);

    public async Task<bool> IsReferencedAsync(int departmentId) =>
        await _db.Users.AnyAsync(u => u.DepartmentId == departmentId)
        || await _db.Assets.AnyAsync(a => a.DepartmentId == departmentId)
        || await _db.AssetAllocations.AnyAsync(a => a.DepartmentId == departmentId)
        || await _db.BudgetForecasts.IgnoreQueryFilters().AnyAsync(b => b.DepartmentId == departmentId);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
