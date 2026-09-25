using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IDepartmentRepository
{
    Task<List<Department>> GetAllAsync();
    Task<Department?> GetByIdAsync(int id);
    Task<Department?> GetByNameAsync(string name);
    Task AddAsync(Department department);
    void Update(Department department);
    void Remove(Department department);

    // UC-04 E1: phòng ban đang được người dùng / tài sản / phân bổ / dự báo ngân sách tham chiếu.
    Task<bool> IsReferencedAsync(int departmentId);
    Task<int> SaveChangesAsync();
}
