using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(List<User> Items, int TotalItems)> GetPagedAsync(int page, int pageSize, string? search)
    {
        var query = _db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // AsNoTracking an toàn dù kết quả có thể bị sửa rồi lưu lại (UpdateAsync) — Update(user) bên dưới
    // luôn gọi _db.Users.Update(user) tường minh, không phụ thuộc change-tracking ngầm.
    public Task<User?> GetByIdWithDetailsAsync(int id) =>
        _db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByEmailAsync(string email) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

    public async Task AddAsync(User user) => await _db.Users.AddAsync(user);

    public void Update(User user) => _db.Users.Update(user);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<int> CountActiveUsersInRoleAsync(int roleId, int? excludeUserId = null) =>
        _db.Users.CountAsync(u =>
            u.RoleId == roleId && u.IsActive && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
}
