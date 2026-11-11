using ITAM.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

/// <summary>
/// Mọi tài khoản đăng nhập phải gắn với MỘT hồ sơ trong Danh mục nhân viên (1-1). Chạy ở mọi môi trường sau khi seed/bootstrap, idempotent:
/// tài khoản chưa có hồ sơ (dữ liệu mẫu, Admin IT khởi tạo từ cấu hình, tài khoản tạo trước khi có quy tắc này) được nối với hồ sơ có
/// sẵn trùng email, nếu không có thì tạo hồ sơ mới từ chính thông tin tài khoản.
/// </summary>
public static class EmployeeAccountLinker
{
    public static async Task EnsureAsync(AppDbContext db, ILogger logger)
    {
        var unlinked = await db.Users.Include(u => u.Role)
            .Where(u => !u.IsDeleted && !db.Employees.Any(e => e.UserId == u.Id))
            .OrderBy(u => u.Id)
            .ToListAsync();
        if (unlinked.Count == 0)
        {
            return;
        }

        var free = await db.Employees.Where(e => e.UserId == null).ToListAsync();
        var takenNames = (await db.Employees.Select(e => new { e.DepartmentId, e.FullName }).ToListAsync())
            .Select(e => (e.DepartmentId, e.FullName)).ToHashSet();

        var created = new List<Employee>();
        var linked = 0;
        foreach (var user in unlinked)
        {
            var match = free.FirstOrDefault(e => string.Equals(e.Email, user.Email, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                match.UserId = user.Id;
                free.Remove(match);
                linked++;
                continue;
            }

            // (Phòng ban, họ tên) là duy nhất trong danh mục: trùng với một nhân viên khác thì thêm email để phân biệt.
            var name = user.FullName;
            if (takenNames.Contains((user.DepartmentId, name)))
            {
                name = $"{user.FullName} ({user.Email})";
                name = name.Length > 100 ? name[..100] : name;
            }

            takenNames.Add((user.DepartmentId, name));
            var employee = new Employee
            {
                EmployeeCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                FullName = name,
                DepartmentId = user.DepartmentId,
                Position = PositionFor(user.Role?.Name),
                Email = user.Email,
                Phone = user.Phone,
                UserId = user.Id,
                IsActive = user.IsActive,
                CreatedAt = DateTime.UtcNow,
            };
            created.Add(employee);
            db.Employees.Add(employee);
        }

        await db.SaveChangesAsync();
        foreach (var employee in created)
        {
            employee.EmployeeCode = $"NV{employee.Id:D4}";
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Đã liên kết tài khoản với Danh mục nhân viên: {Linked} nối hồ sơ có sẵn, {Created} hồ sơ mới.", linked, created.Count);
    }

    private static string? PositionFor(string? roleName) => roleName switch
    {
        "Admin IT" => "Quản trị hệ thống",
        "Manager" => "Quản lý phòng ban",
        "Technician" => "Kỹ thuật viên",
        _ => null,
    };
}
