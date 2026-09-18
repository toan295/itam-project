using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IDepartmentRepository
{
    Task<List<Department>> GetAllAsync();
    Task<Department?> GetByIdAsync(int id);
    Task<Department?> GetByNameAsync(string name);
    Task AddAsync(Department department);
    Task<int> SaveChangesAsync();
}
