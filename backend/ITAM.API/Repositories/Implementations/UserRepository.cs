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
            .OrderBy(u => u.RoleId)
            .ThenBy(u => u.FullName)
            .ThenBy(u => u.Id)
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

    public Task<User?> GetByEmailWithRoleAsync(string email) =>
        _db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

    public Task<User?> GetByIdTrackedAsync(int id) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task AddAsync(User user) => await _db.Users.AddAsync(user);

    // Entity đọc bằng AsNoTracking + Include: sau khi Service đổi RoleId/DepartmentId, navigation Role/Department
    // vẫn trỏ giá trị CŨ và EF ưu tiên navigation -> ghi đè lại khoá ngoại, khiến việc đổi vai trò/phòng ban
    // "thành công" (HTTP 200) nhưng không được lưu. Vì vậy chỉ lấy khoá ngoại làm nguồn sự thật: bỏ navigation
    // rồi đánh dấu duy nhất user này là Modified (không lan sang Role/Department như Users.Update()).
    public void Update(User user)
    {
        user.Role = null!;
        user.Department = null!;
        var entry = _db.Entry(user);
        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<int> CountActiveUsersInRoleAsync(int roleId, int? excludeUserId = null) =>
        _db.Users.CountAsync(u =>
            u.RoleId == roleId && u.IsActive && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
}
