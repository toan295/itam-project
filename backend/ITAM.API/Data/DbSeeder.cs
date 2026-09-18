using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

public static class DbSeeder
{
    private const int BCryptWorkFactor = 11;
    private const string AdminEmail = "admin@eaims.local";
    private const string AdminPassword = "Admin@123";

    // Mật khẩu dùng chung cho các tài khoản demo Manager/Technician bên dưới — chỉ phục vụ
    // seed dữ liệu Development để test nhanh (đăng nhập thử từng vai trò), KHÔNG áp dụng cho
    // tài khoản thật: tài khoản thật do Admin IT tạo qua UsersController sẽ không có mật khẩu
    // biết trước (random hash), người dùng tự đặt qua link thiết lập.
    private const string DemoPassword = "Demo@123";

    public static async Task SeedAsync(AppDbContext context)
    {
        await SeedRolesAsync(context);
        await SeedDepartmentsAsync(context);
        await SeedAssetCategoriesAsync(context);
        await SeedAdminUserAsync(context);

        // Bộ dữ liệu demo mở rộng — phục vụ trình bày/test giao diện đầy đủ chức năng.
        // Ghi chú cho Hoàng Đức Tú: đây là phần thêm mới (không đổi 4 hàm seed nền tảng phía trên),
        // chỉ chạy ở môi trường Development (xem Program.cs), an toàn cho Production.
        await SeedDemoUsersAsync(context);
        await SeedDemoAssetsAsync(context);
        await SeedDemoSoftwareLicensesAsync(context);
        await SeedDemoAssetLicenseAssignmentsAsync(context);
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
            },
            new Department
            {
                Name = "Phong Kinh doanh",
                Description = "Phòng phụ trách kinh doanh và chăm sóc khách hàng."
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
            "Switch/Router",
            "Dien thoai di dong",
            "May chieu"
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

    private static async Task SeedDemoUsersAsync(AppDbContext context)
    {
        var roleIdByName = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);

        var demoUsers = new[]
        {
            new { FullName = "Nguyen Van Quan Ly", Email = "manager.it@eaims.local", Role = "Manager", Department = "Phong IT" },
            new { FullName = "Tran Thi Ke Toan Truong", Email = "manager.ketoan@eaims.local", Role = "Manager", Department = "Phong Ke toan" },
            new { FullName = "Hoang Van Kinh Doanh", Email = "manager.kinhdoanh@eaims.local", Role = "Manager", Department = "Phong Kinh doanh" },
            new { FullName = "Le Van Ky Thuat", Email = "tech.it@eaims.local", Role = "Technician", Department = "Phong IT" },
            new { FullName = "Pham Thi Ho Tro", Email = "tech.nhansu@eaims.local", Role = "Technician", Department = "Phong Nhan su" },
        };

        foreach (var demoUser in demoUsers)
        {
            if (await context.Users.AnyAsync(u => u.Email == demoUser.Email))
            {
                continue;
            }

            context.Users.Add(new User
            {
                FullName = demoUser.FullName,
                Email = demoUser.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword, BCryptWorkFactor),
                RoleId = roleIdByName[demoUser.Role],
                DepartmentId = departmentIdByName[demoUser.Department],
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoAssetsAsync(AppDbContext context)
    {
        var categoryIdByName = await context.AssetCategories.ToDictionaryAsync(c => c.Name, c => c.Id);
        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);

        var demoAssets = new[]
        {
            // --- Bộ gốc (May tinh / Man hinh / May in / Switch) ---
            new AssetSeed("PC-001", "Dell OptiPlex 7090", "May tinh", "Phong IT", AssetStatus.InUse, "SN-PC001", null, "Windows 11 Pro", new DateOnly(2024, 3, 10), new DateOnly(2027, 3, 10)),
            new AssetSeed("PC-002", "Dell Latitude 5420", "May tinh", "Phong Ke toan", AssetStatus.InUse, "SN-PC002", null, "Windows 11 Pro", new DateOnly(2023, 6, 15), new DateOnly(2026, 6, 15)),
            new AssetSeed("PC-003", "HP EliteBook 840", "May tinh", "Phong Nhan su", AssetStatus.InUse, "SN-PC003", null, "Windows 10 Pro", new DateOnly(2021, 1, 20), new DateOnly(2024, 1, 20)),
            new AssetSeed("PC-004", "Lenovo ThinkCentre M70q", "May tinh", "Phong IT", AssetStatus.Maintenance, null, null, "Windows 11 Pro", new DateOnly(2023, 11, 1), new DateOnly(2026, 11, 1)),
            new AssetSeed("PC-005", "Dell Vostro 3400", "May tinh", "Phong Ke toan", AssetStatus.Broken, null, null, "Windows 10 Home", new DateOnly(2020, 5, 12), new DateOnly(2023, 5, 12)),
            new AssetSeed("MH-001", "Man hinh Dell UltraSharp 24 inch", "Man hinh", "Phong IT", AssetStatus.InUse, "SN-MH001", null, null, new DateOnly(2024, 1, 15), new DateOnly(2027, 1, 15)),
            new AssetSeed("MH-002", "Man hinh Samsung Odyssey", "Man hinh", "Phong Nhan su", AssetStatus.InUse, "SN-MH002", null, null, new DateOnly(2022, 7, 1), new DateOnly(2025, 7, 1)),
            new AssetSeed("MI-001", "May in Canon LBP2900", "May in", "Phong Ke toan", AssetStatus.InUse, "SN-MI001", null, null, new DateOnly(2023, 2, 20), new DateOnly(2025, 2, 20)),
            new AssetSeed("MI-002", "May in HP LaserJet Pro", "May in", "Phong IT", AssetStatus.Disposed, "SN-MI002", null, null, new DateOnly(2019, 8, 1), new DateOnly(2021, 8, 1)),
            new AssetSeed("SW-001", "Cisco Catalyst 2960", "Switch/Router", "Phong IT", AssetStatus.InUse, "SN-SW001", null, null, new DateOnly(2022, 9, 1), new DateOnly(2025, 9, 1)),

            // --- Bộ mở rộng: thêm phòng ban/danh mục mới, đa dạng tình trạng bảo hành ---
            new AssetSeed("PC-006", "Dell Latitude 5430", "May tinh", "Phong Kinh doanh", AssetStatus.InUse, "SN-PC006", "RAM 16GB, SSD 512GB", "Windows 11 Pro", new DateOnly(2025, 1, 10), new DateOnly(2028, 1, 10)),
            new AssetSeed("PC-007", "MacBook Pro 14 M3", "May tinh", "Phong Kinh doanh", AssetStatus.InUse, "SN-PC007", "RAM 18GB, SSD 1TB", "macOS Sonoma", new DateOnly(2024, 9, 1), new DateOnly(2026, 9, 1)),
            new AssetSeed("LT-001", "Dell XPS 13", "May tinh", "Phong Nhan su", AssetStatus.Maintenance, "SN-LT001", null, "Windows 11 Home", new DateOnly(2023, 4, 1), new DateOnly(2026, 4, 1)),
            new AssetSeed("DT-001", "iPhone 13", "Dien thoai di dong", "Phong Kinh doanh", AssetStatus.InUse, "SN-DT001", "128GB, mau xanh", "iOS 17", new DateOnly(2024, 11, 20), new DateOnly(2025, 11, 20)),
            new AssetSeed("DT-002", "Samsung Galaxy S23", "Dien thoai di dong", "Phong IT", AssetStatus.InUse, "SN-DT002", "256GB", "Android 14", new DateOnly(2025, 8, 1), new DateOnly(2027, 8, 1)),
            new AssetSeed("DT-003", "Xiaomi Redmi Note 12", "Dien thoai di dong", "Phong Ke toan", AssetStatus.Broken, "SN-DT003", null, "Android 13", new DateOnly(2022, 3, 15), new DateOnly(2024, 3, 15)),
            new AssetSeed("MC-001", "May chieu Epson EB-X05", "May chieu", "Phong Nhan su", AssetStatus.InUse, "SN-MC001", "3300 Lumens", null, new DateOnly(2023, 10, 5), new DateOnly(2026, 10, 5)),
            new AssetSeed("MC-002", "May chieu BenQ MW612", "May chieu", "Phong IT", AssetStatus.Disposed, "SN-MC002", null, null, new DateOnly(2018, 6, 1), new DateOnly(2020, 6, 1)),
            new AssetSeed("SW-002", "TP-Link Switch 24-port", "Switch/Router", "Phong Ke toan", AssetStatus.InUse, "SN-SW002", "24 cong Gigabit", null, new DateOnly(2024, 5, 20), new DateOnly(2027, 5, 20)),
            // Không có ngày mua/bảo hành — mô phỏng tài sản cũ/được bàn giao không rõ nguồn gốc mua sắm.
            new AssetSeed("MH-003", "Man hinh LG UltraGear", "Man hinh", "Phong Kinh doanh", AssetStatus.InUse, "SN-MH003", null, null, null, null),
        };

        foreach (var seed in demoAssets)
        {
            if (await context.Assets.AnyAsync(a => a.AssetCode == seed.AssetCode))
            {
                continue;
            }

            context.Assets.Add(new Asset
            {
                AssetCode = seed.AssetCode,
                Name = seed.Name,
                CategoryId = categoryIdByName[seed.CategoryName],
                DepartmentId = departmentIdByName[seed.DepartmentName],
                Status = seed.Status,
                SerialNumber = seed.SerialNumber,
                Specification = seed.Specification,
                OperatingSystem = seed.OperatingSystem,
                PurchaseDate = seed.PurchaseDate,
                WarrantyExpiry = seed.WarrantyExpiry,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoSoftwareLicensesAsync(AppDbContext context)
    {
        var demoLicenses = new[]
        {
            new LicenseSeed("Microsoft 365 E3", "MS365-E3-XXXX-0001", new DateOnly(2027, 9, 16), 10, null),
            new LicenseSeed("Adobe Creative Cloud", "ADOBE-CC-TEAM-0002", new DateOnly(2026, 10, 5), 5, null),
            // Đã hết hạn tại thời điểm seed — minh hoạ đúng UC-09 E2: hết hạn chỉ cảnh báo, không chặn gán.
            new LicenseSeed("JetBrains All Products Pack", "JB-ALLPROD-0003", new DateOnly(2026, 8, 1), 3, null),
            new LicenseSeed("Windows Server 2022 CAL", "WINSRV-CAL-0004", new DateOnly(2027, 1, 1), 5, null),
            new LicenseSeed("Kaspersky Endpoint Security", "KASP-ENDPT-0005", new DateOnly(2028, 1, 1), 20, null),
            // Sắp hết hạn trong vòng 30 ngày — minh hoạ UC-10 (cảnh báo sắp hết hạn).
            new LicenseSeed("Zoom Business", "ZOOM-BIZ-0006", new DateOnly(2026, 10, 10), 15, null),
            new LicenseSeed("Slack Pro", "SLACK-PRO-0007", new DateOnly(2026, 9, 30), 8, null),
            // MaxUsage nhỏ, sẽ được gán tới 80% — minh hoạ cảnh báo "gần vượt hạn mức" (IsNearUsageLimit).
            new LicenseSeed("AutoCAD 2026", "ACAD-2026-0008", new DateOnly(2027, 5, 1), 5, "Chi cap cho phong ban co nhu cau thiet ke."),
        };

        foreach (var seed in demoLicenses)
        {
            if (await context.SoftwareLicenses.AnyAsync(l => l.LicenseKey == seed.LicenseKey))
            {
                continue;
            }

            context.SoftwareLicenses.Add(new SoftwareLicense
            {
                SoftwareName = seed.SoftwareName,
                LicenseKey = seed.LicenseKey,
                ExpiryDate = seed.ExpiryDate,
                MaxUsage = seed.MaxUsage,
                Notes = seed.Notes
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedDemoAssetLicenseAssignmentsAsync(AppDbContext context)
    {
        var assetIdByCode = await context.Assets.ToDictionaryAsync(a => a.AssetCode, a => a.Id);
        var licenseIdByKey = await context.SoftwareLicenses.ToDictionaryAsync(l => l.LicenseKey, l => l.Id);

        var assignments = new[]
        {
            // Bộ gốc (khớp dữ liệu đã gán thủ công trước đó).
            ("PC-001", "MS365-E3-XXXX-0001"),
            ("PC-002", "MS365-E3-XXXX-0001"),
            ("PC-003", "MS365-E3-XXXX-0001"),
            ("PC-001", "WINSRV-CAL-0004"),
            ("PC-004", "WINSRV-CAL-0004"),
            ("MH-001", "WINSRV-CAL-0004"),
            ("SW-001", "WINSRV-CAL-0004"),
            ("PC-001", "KASP-ENDPT-0005"),
            ("PC-002", "KASP-ENDPT-0005"),

            // Bộ mở rộng.
            ("PC-001", "ADOBE-CC-TEAM-0002"),
            ("PC-006", "ADOBE-CC-TEAM-0002"),
            ("PC-007", "ADOBE-CC-TEAM-0002"),
            ("PC-001", "JB-ALLPROD-0003"),
            ("PC-002", "JB-ALLPROD-0003"),
            ("PC-001", "ZOOM-BIZ-0006"),
            ("PC-002", "ZOOM-BIZ-0006"),
            ("PC-006", "ZOOM-BIZ-0006"),
            ("PC-007", "ZOOM-BIZ-0006"),
            ("DT-002", "ZOOM-BIZ-0006"),
            ("MC-001", "ZOOM-BIZ-0006"),
            ("PC-001", "SLACK-PRO-0007"),
            ("PC-003", "SLACK-PRO-0007"),
            ("PC-006", "SLACK-PRO-0007"),
            ("DT-002", "SLACK-PRO-0007"),
            ("PC-002", "ACAD-2026-0008"),
            ("PC-006", "ACAD-2026-0008"),
            ("PC-007", "ACAD-2026-0008"),
            ("LT-001", "ACAD-2026-0008"),
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var (assetCode, licenseKey) in assignments)
        {
            if (!assetIdByCode.TryGetValue(assetCode, out var assetId) ||
                !licenseIdByKey.TryGetValue(licenseKey, out var licenseId))
            {
                continue;
            }

            var alreadyAssigned = await context.AssetSoftwareLicenses
                .AnyAsync(x => x.AssetId == assetId && x.LicenseId == licenseId);
            if (alreadyAssigned)
            {
                continue;
            }

            context.AssetSoftwareLicenses.Add(new AssetSoftwareLicense
            {
                AssetId = assetId,
                LicenseId = licenseId,
                AssignedDate = today
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private sealed record AssetSeed(
        string AssetCode,
        string Name,
        string CategoryName,
        string DepartmentName,
        AssetStatus Status,
        string? SerialNumber,
        string? Specification,
        string? OperatingSystem,
        DateOnly? PurchaseDate,
        DateOnly? WarrantyExpiry);

    private sealed record LicenseSeed(
        string SoftwareName,
        string LicenseKey,
        DateOnly ExpiryDate,
        int MaxUsage,
        string? Notes);
}
