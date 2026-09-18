using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IUserRepository
{
    Task<(List<User> Items, int TotalItems)> GetPagedAsync(int page, int pageSize, string? search);
    Task<User?> GetByIdWithDetailsAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
    void Update(User user);
    Task<int> SaveChangesAsync();

    // Đếm số Admin IT đang active — dùng để chặn khoá/hạ quyền Admin IT cuối cùng của hệ thống.
    Task<int> CountActiveUsersInRoleAsync(int roleId, int? excludeUserId = null);
}
