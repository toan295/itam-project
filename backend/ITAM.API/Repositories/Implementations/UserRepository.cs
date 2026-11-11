using ITAM.API.Models.DTOs.Users;
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

    public async Task<(List<User> Items, int TotalItems)> GetPagedAsync(int page, int pageSize, UserListFilter f)
    {
        var query = _db.Users.AsNoTracking().Where(u => !u.IsDeleted).Include(u => u.Role).Include(u => u.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var k = f.Search.Trim();
            var phoneKey = System.Text.RegularExpressions.Regex.Replace(k, @"[\s.\-()]", "");
            var phoneSearch = phoneKey.Length > 0 && phoneKey.All(c => char.IsDigit(c) || c == '+');
            query = query.Where(u => u.FullName.Contains(k) || u.Email.Contains(k)
                || (phoneSearch && u.Phone != null && u.Phone.Contains(phoneKey))
                || _db.Employees.Any(e => e.UserId == u.Id && e.EmployeeCode.Contains(k)));
        }

        if (f.RoleId.HasValue)
        {
            query = query.Where(u => u.RoleId == f.RoleId.Value);
        }

        if (f.DepartmentId.HasValue)
        {
            query = query.Where(u => u.DepartmentId == f.DepartmentId.Value);
        }

        if (f.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == f.IsActive.Value);
        }

        if (f.Activation == "pending")
        {
            query = query.Where(u => u.PendingActivation);
        }
        else if (f.Activation == "activated")
        {
            query = query.Where(u => !u.PendingActivation);
        }

        var total = await query.CountAsync();
        var ordered = f.Sort switch
        {
            "name" => query.OrderBy(u => u.FullName),
            "newest" => query.OrderByDescending(u => u.Id),
            _ => query.OrderBy(u => u.RoleId).ThenBy(u => u.FullName),
        };
        var items = await ordered
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    // AsNoTracking an toàn dù kết quả có thể bị sửa rồi lưu lại (UpdateAsync) — Update(user) bên dưới
    // luôn gọi _db.Users.Update(user) tường minh, không phụ thuộc change-tracking ngầm.
    public Task<User?> GetByIdWithDetailsAsync(int id) =>
        _db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

    public Task<User?> GetByEmailAsync(string email) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

    public Task<User?> GetByEmailWithRoleAsync(string email) =>
        _db.Users.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

    public Task<User?> GetByIdTrackedAsync(int id) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

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

    public Task<int> CountPendingAssignedTicketsAsync(int userId) =>
        _db.MaintenanceTickets.CountAsync(t => t.TechnicianId == userId && t.Status == ITAM.API.Models.Enums.TicketStatus.Pending);

    public Task<int> CountActiveUsersInRoleAsync(int roleId, int? excludeUserId = null) =>
        _db.Users.CountAsync(u =>
            u.RoleId == roleId && u.IsActive && !u.IsDeleted && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
}
