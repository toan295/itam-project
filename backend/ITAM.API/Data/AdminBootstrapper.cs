using System.Net.Mail;
using ITAM.API.Helpers;
using ITAM.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

// Tạo tài khoản Admin IT đầu tiên cho môi trường KHÔNG phải Development (DbSeeder chỉ chạy ở Development nên ở môi
// trường thật sẽ không có ai đăng nhập được). Kích hoạt bằng cấu hình (nên truyền qua biến môi trường, không commit):
//   Bootstrap__AdminEmail, Bootstrap__AdminPassword, [Bootstrap__AdminFullName], [Bootstrap__DepartmentName]
// An toàn khi chạy lại: nếu đã có ít nhất một Admin IT đang hoạt động thì KHÔNG làm gì (không bao giờ ghi đè hay
// đặt lại mật khẩu). Tài khoản tạo ra bị buộc đổi mật khẩu ở lần đăng nhập đầu; sau đó nên xoá cấu hình Bootstrap.
public static class AdminBootstrapper
{
    private const int BCryptWorkFactor = 11;
    private const string AdminRoleName = "Admin IT";
    private static readonly string[] RoleNames = { AdminRoleName, "Manager", "Technician" };

    public static async Task RunAsync(AppDbContext db, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Bootstrap:AdminEmail"]?.Trim();
        var password = configuration["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrEmpty(password))
        {
            return; // không cấu hình -> không làm gì.
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException(
                "Bootstrap:AdminEmail và Bootstrap:AdminPassword phải được cấu hình cùng nhau.");
        }

        if (await db.Users.AnyAsync(u => u.IsActive && u.Role.Name == AdminRoleName))
        {
            logger.LogInformation("Đã có tài khoản Admin IT hoạt động — bỏ qua cấu hình Bootstrap (nên xoá cấu hình này).");
            return;
        }

        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || email.Length > 150)
        {
            throw new InvalidOperationException("Bootstrap:AdminEmail không phải email hợp lệ.");
        }

        var policyError = PasswordPolicy.Validate(password);
        if (policyError is not null)
        {
            throw new InvalidOperationException($"Bootstrap:AdminPassword không đạt chính sách mật khẩu: {policyError}");
        }

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            throw new InvalidOperationException(
                $"Email '{email}' đã tồn tại nhưng không phải Admin IT đang hoạt động; dùng email khác hoặc nhờ Admin khác mở lại tài khoản.");
        }

        foreach (var roleName in RoleNames)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == roleName))
            {
                db.Roles.Add(new Role { Name = roleName });
            }
        }

        var departmentName = configuration["Bootstrap:DepartmentName"]?.Trim();
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            departmentName = "Phòng IT";
        }

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Name == departmentName);
        if (department is null)
        {
            department = new Department { Name = departmentName, Description = "Phòng phụ trách hạ tầng và hỗ trợ công nghệ thông tin." };
            db.Departments.Add(department);
        }

        await db.SaveChangesAsync(); // có Id của role/phòng ban cho bước tạo user.

        var adminRole = await db.Roles.SingleAsync(r => r.Name == AdminRoleName);
        var fullName = configuration["Bootstrap:AdminFullName"]?.Trim();

        db.Users.Add(new User
        {
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Quản trị hệ thống" : fullName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BCryptWorkFactor),
            RoleId = adminRole.Id,
            DepartmentId = department.Id,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Không bao giờ ghi mật khẩu vào log.
        logger.LogWarning("Đã tạo tài khoản Admin IT đầu tiên {Email} từ cấu hình Bootstrap. Hãy xoá cấu hình Bootstrap sau khi đăng nhập và đổi mật khẩu.", email);
    }
}
