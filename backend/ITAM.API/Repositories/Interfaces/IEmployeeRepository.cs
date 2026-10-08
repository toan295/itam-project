using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IEmployeeRepository
{
    // Tracked, kèm Department — Service sửa trực tiếp rồi SaveChangesAsync.
    Task<Employee?> GetByIdAsync(int id);

    Task<(List<Employee> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, string? keyword, bool? isActive, int page, int pageSize);

    Task<bool> NameExistsInDepartmentAsync(string fullName, int departmentId, int? excludeId = null);
    Task<bool> DepartmentExistsAsync(int departmentId);
    Task<bool> HasOpenAllocationAsync(int employeeId);
    Task<bool> HasAnyAllocationAsync(int employeeId);
    Task AddAsync(Employee employee);
    void Remove(Employee employee);
    Task<int> SaveChangesAsync();
}
