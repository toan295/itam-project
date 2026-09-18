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

    public Task<List<Department>> GetAllAsync() =>
        _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();

    public Task<Department?> GetByIdAsync(int id) =>
        _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

    public Task<Department?> GetByNameAsync(string name) =>
        _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Name == name);

    public async Task AddAsync(Department department) => await _db.Departments.AddAsync(department);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
