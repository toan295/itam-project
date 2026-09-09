using ITAM.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

public static class DbSeeder
{
    private const int BCryptWorkFactor = 11;
    private const string AdminEmail = "admin@eaims.local";
    private const string AdminPassword = "Admin@123";

    public static async Task SeedAsync(AppDbContext context)
    {
        await SeedRolesAsync(context);
        await SeedDepartmentsAsync(context);
        await SeedAssetCategoriesAsync(context);
        await SeedAdminUserAsync(context);
    }

    private static async Task SeedRolesAsync(AppDbContext context)
    {
        var roleNames = new[] { "Admin IT", "Manager", "Technician" };

        foreach (var roleName in roleNames)
        {
            if (!await context.Roles.AnyAsync(role => role.Name == roleName))
            {
                context.Roles.Add(new Role { Name = roleName });
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedDepartmentsAsync(AppDbContext context)
    {
        var departments = new[]
        {
            new Department
            {
                Name = "Phong IT",
                Description = "Phòng phụ trách hạ tầng và hỗ trợ công nghệ thông tin."
            },
            new Department
            {
                Name = "Phong Ke toan",
                Description = "Phòng phụ trách kế toán và tài chính."
            },
            new Department
            {
                Name = "Phong Nhan su",
                Description = "Phòng phụ trách nhân sự."
            }
        };

        foreach (var department in departments)
        {
            if (!await context.Departments.AnyAsync(item => item.Name == department.Name))
            {
                context.Departments.Add(department);
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAssetCategoriesAsync(AppDbContext context)
    {
        var categoryNames = new[]
        {
            "May tinh",
            "Man hinh",
            "May in",
            "Switch/Router"
        };

        foreach (var categoryName in categoryNames)
        {
            if (!await context.AssetCategories.AnyAsync(category => category.Name == categoryName))
            {
                context.AssetCategories.Add(new AssetCategory { Name = categoryName });
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAdminUserAsync(AppDbContext context)
    {
        if (await context.Users.AnyAsync(user => user.Email == AdminEmail))
        {
            return;
        }

        var adminRole = await context.Roles
            .SingleAsync(role => role.Name == "Admin IT");
        var itDepartment = await context.Departments
            .SingleAsync(department => department.Name == "Phong IT");

        var admin = new User
        {
            FullName = "EAIMS Admin",
            Email = AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, BCryptWorkFactor),
            RoleId = adminRole.Id,
            DepartmentId = itDepartment.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}
