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
        await SeedDemoMaintenanceTicketsAsync(context);
        await SeedDemoAssetAllocationsAsync(context);
        // Chưa seed BudgetForecasts / AuditLogs / ImportLogs: chưa có module nào đọc/ghi các bảng này
        // (UC-17 Tuần 7-8, AuditLog Service và Import Tuần 9+) — seed sớm dễ lệch với thiết kế sau này.
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
            },
            new Department
            {
                Name = "Phong Marketing",
                Description = "Phòng phụ trách truyền thông, thương hiệu và sự kiện."
            },
            // Cố ý KHÔNG gán người dùng/tài sản nào — dùng để thử chức năng Xoá phòng ban (UC-04):
            // phòng ban đang có dữ liệu tham chiếu sẽ bị từ chối xoá, phòng ban trống này thì xoá được.
            new Department
            {
                Name = "Phong Hanh chinh",
                Description = "Phòng hành chính - văn thư (chưa có nhân sự/tài sản, dùng để thử xoá phòng ban)."
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
            "May chieu",
            "May chu",
            "May quet",
            // Cố ý chưa có tài sản nào dùng — để thử chức năng Xoá loại tài sản (UC-04).
            "Phu kien"
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
            new { FullName = "Nguyen Van Ly", Email = "manager.it@eaims.local", Role = "Manager", Department = "Phong IT", Active = true },
            new { FullName = "Tran Thi Truong", Email = "manager.ketoan@eaims.local", Role = "Manager", Department = "Phong Ke toan", Active = true },
            new { FullName = "Hoang Van Doanh", Email = "manager.kinhdoanh@eaims.local", Role = "Manager", Department = "Phong Kinh doanh", Active = true },
            new { FullName = "Le Van Thuat", Email = "tech.it@eaims.local", Role = "Technician", Department = "Phong IT", Active = true },
            new { FullName = "Pham Thi Tro", Email = "tech.nhansu@eaims.local", Role = "Technician", Department = "Phong Nhan su", Active = true },

            // --- Bộ mở rộng: đủ Manager/Technician cho từng phòng ban + Admin thứ 2 + 1 tài khoản bị khoá ---
            new { FullName = "Bui Quoc Bao", Email = "admin.phu@eaims.local", Role = "Admin IT", Department = "Phong IT", Active = true },
            new { FullName = "Vu Thi Lan", Email = "manager.nhansu@eaims.local", Role = "Manager", Department = "Phong Nhan su", Active = true },
            new { FullName = "Dang Minh Quan", Email = "manager.marketing@eaims.local", Role = "Manager", Department = "Phong Marketing", Active = true },
            new { FullName = "Ngo Van Hieu", Email = "tech.it2@eaims.local", Role = "Technician", Department = "Phong IT", Active = true },
            new { FullName = "Do Thi Mai", Email = "tech.ketoan@eaims.local", Role = "Technician", Department = "Phong Ke toan", Active = true },
            new { FullName = "Trinh Van Nam", Email = "tech.kinhdoanh@eaims.local", Role = "Technician", Department = "Phong Kinh doanh", Active = true },
            new { FullName = "Ly Thi Hoa", Email = "tech.marketing@eaims.local", Role = "Technician", Department = "Phong Marketing", Active = true },
            // Tài khoản đã bị khoá (IsActive = false) — minh hoạ UC-01 E2 và chức năng khoá/mở khoá của Admin (UC-03).
            new { FullName = "Cao Van Tung", Email = "nghi.viec@eaims.local", Role = "Technician", Department = "Phong Nhan su", Active = false },
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
                IsActive = demoUser.Active,
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

            // --- Bộ mở rộng 2: đủ 5 phòng ban có tài sản, thêm loại May chu / May quet, đa dạng trạng thái/bảo hành ---
            // Phong IT
            new AssetSeed("SV-001", "Dell PowerEdge R750", "May chu", "Phong IT", AssetStatus.InUse, "SN-SV001", "2x Xeon Silver, RAM 128GB, RAID10 4TB", "Windows Server 2022", today.AddDays(-400), today.AddDays(700)),
            new AssetSeed("SV-002", "HPE ProLiant DL380 Gen10", "May chu", "Phong IT", AssetStatus.InUse, "SN-SV002", "Xeon Gold, RAM 64GB, SSD 2TB", "Ubuntu Server 22.04", new DateOnly(2022, 4, 10), new DateOnly(2025, 4, 10)),
            new AssetSeed("SV-003", "Dell PowerEdge T350", "May chu", "Phong IT", AssetStatus.InUse, "SN-SV003", "Xeon E-2334, RAM 32GB", "Windows Server 2019", new DateOnly(2023, 8, 20), new DateOnly(2026, 8, 20)),
            new AssetSeed("PC-008", "Lenovo ThinkPad T14", "May tinh", "Phong IT", AssetStatus.InUse, "SN-PC008", "Ryzen 7, RAM 16GB, SSD 512GB", "Windows 11 Pro", today.AddDays(-90), today.AddDays(1000)),
            // Bảo hành sắp hết trong 20 ngày — minh hoạ lọc "còn bảo hành" ở gần mốc hết hạn.
            new AssetSeed("MH-004", "Man hinh HP E24 G5", "Man hinh", "Phong IT", AssetStatus.InUse, "SN-MH004", "24 inch FHD IPS", null, today.AddDays(-1075), today.AddDays(20)),
            new AssetSeed("SW-003", "Ubiquiti UniFi Switch 48", "Switch/Router", "Phong IT", AssetStatus.InUse, "SN-SW003", "48 cong PoE+", null, new DateOnly(2024, 2, 5), new DateOnly(2027, 2, 5)),
            new AssetSeed("MI-003", "May in Brother HL-L2321D", "May in", "Phong IT", AssetStatus.Broken, "SN-MI003", null, null, new DateOnly(2022, 10, 1), new DateOnly(2024, 10, 1)),
            // Phong Ke toan
            new AssetSeed("PC-009", "HP ProDesk 400 G7", "May tinh", "Phong Ke toan", AssetStatus.InUse, "SN-PC009", "Core i5, RAM 8GB, SSD 256GB", "Windows 10 Pro", new DateOnly(2022, 5, 5), new DateOnly(2025, 5, 5)),
            new AssetSeed("PC-010", "Dell OptiPlex 3080", "May tinh", "Phong Ke toan", AssetStatus.InUse, "SN-PC010", "Core i5, RAM 8GB, HDD 1TB", "Windows 10 Pro", new DateOnly(2021, 11, 15), new DateOnly(2024, 11, 15)),
            new AssetSeed("MH-005", "Man hinh Dell P2422H", "Man hinh", "Phong Ke toan", AssetStatus.InUse, "SN-MH005", "24 inch FHD", null, new DateOnly(2024, 6, 1), new DateOnly(2027, 6, 1)),
            new AssetSeed("SC-001", "May quet Fujitsu fi-7160", "May quet", "Phong Ke toan", AssetStatus.InUse, "SN-SC001", "60 trang/phut, ADF 80 to", null, new DateOnly(2023, 3, 12), new DateOnly(2026, 3, 12)),
            new AssetSeed("MI-004", "May in Epson L3250", "May in", "Phong Ke toan", AssetStatus.InUse, "SN-MI004", "In phun mau, WiFi", null, today.AddDays(-200), today.AddDays(530)),
            // Phong Nhan su
            new AssetSeed("PC-011", "Acer Veriton X2690G", "May tinh", "Phong Nhan su", AssetStatus.InUse, "SN-PC011", "Core i5, RAM 8GB, SSD 256GB", "Windows 11 Pro", new DateOnly(2024, 4, 22), new DateOnly(2027, 4, 22)),
            new AssetSeed("MH-006", "Man hinh AOC 24B1H", "Man hinh", "Phong Nhan su", AssetStatus.InUse, "SN-MH006", "24 inch FHD VA", null, new DateOnly(2023, 1, 9), new DateOnly(2026, 1, 9)),
            new AssetSeed("SC-002", "May quet Canon DR-C225", "May quet", "Phong Nhan su", AssetStatus.InUse, "SN-SC002", "25 trang/phut", null, new DateOnly(2022, 12, 1), new DateOnly(2025, 12, 1)),
            new AssetSeed("MI-005", "May in Brother HL-1110 (cu)", "May in", "Phong Nhan su", AssetStatus.Disposed, "SN-MI005", null, null, new DateOnly(2017, 3, 1), new DateOnly(2019, 3, 1)),
            // Phong Kinh doanh
            new AssetSeed("LT-002", "MacBook Air M2", "May tinh", "Phong Kinh doanh", AssetStatus.InUse, "SN-LT002", "RAM 16GB, SSD 512GB", "macOS Sonoma", new DateOnly(2024, 7, 3), new DateOnly(2026, 7, 3)),
            new AssetSeed("LT-003", "Lenovo IdeaPad 5 Pro", "May tinh", "Phong Kinh doanh", AssetStatus.InUse, "SN-LT003", "Ryzen 5, RAM 16GB, SSD 512GB", "Windows 11 Home", today.AddDays(-60), today.AddDays(670)),
            new AssetSeed("DT-005", "Samsung Galaxy A54", "Dien thoai di dong", "Phong Kinh doanh", AssetStatus.InUse, "SN-DT005", "128GB", "Android 14", new DateOnly(2025, 3, 18), new DateOnly(2027, 3, 18)),
            new AssetSeed("MC-003", "May chieu ViewSonic PA503S", "May chieu", "Phong Kinh doanh", AssetStatus.InUse, "SN-MC003", "3800 Lumens", null, new DateOnly(2023, 9, 14), new DateOnly(2026, 9, 14)),
            // Phong Marketing
            new AssetSeed("PC-012", "iMac 24 inch M3", "May tinh", "Phong Marketing", AssetStatus.InUse, "SN-PC012", "RAM 16GB, SSD 512GB", "macOS Sonoma", today.AddDays(-100), today.AddDays(965)),
            new AssetSeed("LT-004", "MacBook Pro 16 M3 Pro", "May tinh", "Phong Marketing", AssetStatus.InUse, "SN-LT004", "RAM 36GB, SSD 1TB", "macOS Sonoma", today.AddDays(-150), today.AddDays(915)),
            new AssetSeed("MH-007", "Man hinh LG 27UL500", "Man hinh", "Phong Marketing", AssetStatus.InUse, "SN-MH007", "27 inch 4K", null, new DateOnly(2024, 3, 8), new DateOnly(2027, 3, 8)),
            new AssetSeed("DT-007", "Samsung Galaxy S24", "Dien thoai di dong", "Phong Marketing", AssetStatus.InUse, "SN-DT007", "256GB", "Android 14", today.AddDays(-120), today.AddDays(610)),
            new AssetSeed("MC-004", "May chieu Optoma HD146X", "May chieu", "Phong Marketing", AssetStatus.InUse, "SN-MC004", "3600 Lumens Full HD", null, new DateOnly(2023, 5, 25), new DateOnly(2026, 5, 25)),
            new AssetSeed("MI-006", "May in mau Canon G3020", "May in", "Phong Marketing", AssetStatus.InUse, "SN-MI006", "In phun mau, WiFi", null, new DateOnly(2024, 10, 10), new DateOnly(2026, 10, 10)),
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

            // --- Bộ mở rộng (hạn dùng tính theo ngày hiện tại để luôn minh hoạ đúng các trạng thái cảnh báo) ---
            new LicenseSeed("Visual Studio Enterprise 2022", "VS-ENT-2022-0009", new DateOnly(2027, 3, 1), 6, null),
            new LicenseSeed("Microsoft Office LTSC 2021", "OFFICE-LTSC-0010", new DateOnly(2028, 6, 30), 25, "Ban quyen vinh vien theo goi 3 nam."),
            // Sắp hết hạn trong 15 ngày.
            new LicenseSeed("Veeam Backup & Replication", "VEEAM-BR-0011", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), 4, "May chu backup."),
            // Đã hết hạn 10 ngày trước.
            new LicenseSeed("FortiClient VPN", "FORTI-VPN-0012", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10), 10, "Can gia han."),
            // Dùng hết hạn mức (3/3) — minh hoạ UC-09 E1 (gán thêm sẽ bị từ chối).
            new LicenseSeed("Canva Pro", "CANVA-PRO-0013", new DateOnly(2027, 1, 15), 3, "Danh cho phong Marketing."),
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

            // Bộ mở rộng.
            ("PC-008", "VS-ENT-2022-0009"),
            ("PC-001", "VS-ENT-2022-0009"),
            ("PC-004", "VS-ENT-2022-0009"),
            ("PC-009", "OFFICE-LTSC-0010"),
            ("PC-010", "OFFICE-LTSC-0010"),
            ("PC-011", "OFFICE-LTSC-0010"),
            ("PC-012", "OFFICE-LTSC-0010"),
            ("LT-003", "OFFICE-LTSC-0010"),
            ("LT-004", "OFFICE-LTSC-0010"),
            ("SV-001", "VEEAM-BR-0011"),
            ("SV-002", "VEEAM-BR-0011"),
            ("SV-003", "VEEAM-BR-0011"),
            ("LT-002", "FORTI-VPN-0012"),
            ("LT-003", "FORTI-VPN-0012"),
            ("LT-004", "FORTI-VPN-0012"),
            ("DT-005", "FORTI-VPN-0012"),
            ("PC-012", "CANVA-PRO-0013"),
            ("LT-004", "CANVA-PRO-0013"),
            ("DT-007", "CANVA-PRO-0013"),
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


    // Phiếu bảo trì mẫu — bám đúng quy tắc UC-11/UC-12:
    //  - Pending: chưa có ResolvedDate; có thể chưa gán (để Technician tự nhận) hoặc đã gán (kể cả khác phòng ban).
    //  - Resolved: tài sản đang InUse; Failed: tài sản đang Broken; ResolvedDate >= ReportedDate.
    // Mốc thời gian tính theo ngày hiện tại nên thống kê theo năm/thời gian xử lý luôn có dữ liệu "gần đây".
    private static async Task SeedDemoMaintenanceTicketsAsync(AppDbContext context)
    {
        var assetIdByCode = await context.Assets.ToDictionaryAsync(a => a.AssetCode, a => a.Id);
        var technicianIdByEmail = await context.Users
            .Where(u => u.Role.Name == "Technician")
            .ToDictionaryAsync(u => u.Email, u => u.Id);

        var now = DateTime.UtcNow;

        var tickets = new[]
        {
            // --- Pending, chưa gán (Technician tự nhận — UC-12 bước 1) ---
            new TicketSeed("MI-003", "May in bi ket giay lien tuc, khong in duoc.", TicketStatus.Pending, null, 2, null, null),
            new TicketSeed("PC-009", "May khoi dong cham, bi treo khi mo file Excel lon.", TicketStatus.Pending, null, 1, null, null),
            new TicketSeed("MH-006", "Man hinh bi soc doc, mat tin hieu HDMI thinh thoang.", TicketStatus.Pending, null, 3, null, null),
            new TicketSeed("LT-002", "Pin chai, chi dung duoc khoang 30 phut.", TicketStatus.Pending, null, 5, null, null),

            // --- Pending, đã gán ---
            new TicketSeed("SV-002", "Quat tan nhiet keu to, nhiet do CPU cao bat thuong.", TicketStatus.Pending, "tech.it@eaims.local", 4, null, null),
            new TicketSeed("PC-004", "Khong len nguon sau khi mat dien dot ngot.", TicketStatus.Pending, "tech.it2@eaims.local", 6, null, null),
            new TicketSeed("LT-001", "Thay ban phim va kiem tra tinh trang pin.", TicketStatus.Pending, "tech.nhansu@eaims.local", 8, null, null),
            new TicketSeed("DT-005", "Man hinh cam ung bi loan cam ung o goc duoi.", TicketStatus.Pending, "tech.kinhdoanh@eaims.local", 2, null, null),
            // Kỹ thuật viên phòng IT nhận phiếu của phòng Nhân sự (Admin/Manager được gán khác phòng ban).
            new TicketSeed("PC-011", "Cai lai he dieu hanh va cap nhat driver.", TicketStatus.Pending, "tech.it@eaims.local", 1, null, null),

            // --- Resolved (tài sản đang InUse) ---
            new TicketSeed("PC-001", "Cap nhat BIOS va thay pin CMOS.", TicketStatus.Resolved, "tech.it@eaims.local", 40, 6, "Da thay pin CMOS, cap nhat BIOS ban moi nhat."),
            new TicketSeed("PC-002", "Cai lai Windows va sao luu du lieu ke toan.", TicketStatus.Resolved, "tech.ketoan@eaims.local", 25, 30, "Da sao luu du lieu, cai lai Windows 11 va cai phan mem ke toan."),
            new TicketSeed("MH-001", "Man hinh bi nhap nhay khi chay do hoa.", TicketStatus.Resolved, "tech.it2@eaims.local", 12, 3.5, "Doi cap DisplayPort, het nhap nhay."),
            new TicketSeed("MI-001", "Thay drum va ve sinh may in.", TicketStatus.Resolved, "tech.ketoan@eaims.local", 55, 26, "Da thay drum moi."),
            new TicketSeed("PC-006", "May chay cham, nang cap RAM tu 8GB len 16GB.", TicketStatus.Resolved, "tech.kinhdoanh@eaims.local", 70, 48, "Da nang cap RAM."),
            // Phiếu của năm trước — để thống kê "số lần bảo trì theo tài sản-năm" (UC-13) có nhiều năm.
            new TicketSeed("SW-001", "Cap nhat firmware switch len ban moi.", TicketStatus.Resolved, "tech.it@eaims.local", 400, 5, "Cap nhat firmware thanh cong."),
            new TicketSeed("PC-001", "Thay quat tan nhiet CPU bi kem.", TicketStatus.Resolved, "tech.it@eaims.local", 420, 8, "Da thay quat moi."),
            new TicketSeed("PC-003", "Thay o cung SSD, cai lai he dieu hanh.", TicketStatus.Resolved, "tech.nhansu@eaims.local", 380, 20, "Da thay SSD 256GB."),

            // --- Failed (tài sản đang Broken) ---
            new TicketSeed("PC-005", "Mainboard hong, khong the khoi dong.", TicketStatus.Failed, "tech.ketoan@eaims.local", 20, 72, "Chi phi sua vuot gia tri may, de xuat thanh ly."),
            new TicketSeed("DT-003", "Vo man hinh va hong nguon.", TicketStatus.Failed, "tech.it@eaims.local", 33, 50, "Khong the thay the linh kien."),
        };

        foreach (var seed in tickets)
        {
            if (!assetIdByCode.TryGetValue(seed.AssetCode, out var assetId))
            {
                continue;
            }

            int? technicianId = null;
            if (seed.TechnicianEmail is not null)
            {
                if (!technicianIdByEmail.TryGetValue(seed.TechnicianEmail, out var id))
                {
                    continue;
                }

                technicianId = id;
            }

            if (await context.MaintenanceTickets.AnyAsync(t =>
                    t.AssetId == assetId && t.IssueDescription == seed.IssueDescription))
            {
                continue;
            }

            var reported = now.AddDays(-seed.DaysAgo);
            context.MaintenanceTickets.Add(new MaintenanceTicket
            {
                AssetId = assetId,
                IssueDescription = seed.IssueDescription,
                Status = seed.Status,
                TechnicianId = technicianId,
                ReportedDate = reported,
                ResolvedDate = seed.ResolutionHours.HasValue ? reported.AddHours(seed.ResolutionHours.Value) : null,
                Notes = seed.Notes,
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    // Phân bổ/thu hồi mẫu — bám đúng UC-14/UC-15:
    //  - Mỗi tài sản tối đa 1 phân bổ đang mở, và chỉ tài sản InUse mới có phân bổ đang mở.
    //  - Thu hồi "Damaged" thì tài sản đã chuyển Broken (DT-003); ngày thu hồi >= ngày phân bổ.
    //  - Một số tài sản chưa từng bảo trì / bảo trì cách đây > 180 ngày => xuất hiện trong "Cảnh báo quá hạn bảo trì".
    private static async Task SeedDemoAssetAllocationsAsync(AppDbContext context)
    {
        var assetIdByCode = await context.Assets.ToDictionaryAsync(a => a.AssetCode, a => a.Id);
        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var allocations = new[]
        {
            // --- Đang phân bổ (chưa thu hồi) ---
            new AllocationSeed("PC-001", "Phong IT", "Nguyen Van An", 200, null, null, "Ban giao kem chuot, ban phim.", null),
            new AllocationSeed("PC-002", "Phong Ke toan", "Le Thi Hoa", 300, null, null, "Ban giao kem tui chong soc.", null),
            new AllocationSeed("PC-003", "Phong Nhan su", "Tran Van Binh", 500, null, null, null, null),
            new AllocationSeed("MH-002", "Phong Nhan su", "Pham Thi Cuc", 150, null, null, null, null),
            new AllocationSeed("PC-006", "Phong Kinh doanh", "Hoang Van Duc", 100, null, null, "Ban giao kem sac va balo.", null),
            new AllocationSeed("PC-007", "Phong Kinh doanh", "Dang Thi Em", 90, null, null, null, null),
            new AllocationSeed("LT-003", "Phong Kinh doanh", "Vu Van Phuc", 50, null, null, "Ban giao kem sac.", null),
            new AllocationSeed("PC-012", "Phong Marketing", "Nguyen Thi Phuong", 30, null, null, null, null),
            new AllocationSeed("SV-001", "Phong IT", "Doi van hanh ha tang", 20, null, null, "May chu chay he thong noi bo.", null),
            // Tài sản của phòng Kinh doanh nhưng cho phòng Marketing mượn dài hạn.
            new AllocationSeed("MC-003", "Phong Marketing", "Nhom To chuc su kien", 10, null, null, "Muon cho chuong trinh ra mat san pham.", null),
            // DT-001: đã thu hồi 1 lần (Good) rồi được phân bổ lại cho người khác.
            new AllocationSeed("DT-001", "Phong Kinh doanh", "Bui Van Long", 140, null, null, null, null),

            // --- Đã thu hồi ---
            new AllocationSeed("DT-001", "Phong Kinh doanh", "Ly Thi Kim", 300, 150, AssetReturnCondition.Good, null, "Nhan vien nghi viec, may hoat dong tot."),
            new AllocationSeed("PC-008", "Phong IT", "Vo Van Khoa", 120, 30, AssetReturnCondition.Good, null, "Thu hoi de cap phat cho nhan vien moi."),
            new AllocationSeed("MH-004", "Phong IT", "Cao Thi Lien", 250, 60, AssetReturnCondition.Good, null, "Doi man hinh lon hon."),
            new AllocationSeed("PC-010", "Phong Ke toan", "Mai Van Nghia", 400, 90, AssetReturnCondition.Good, null, null),
            // Thu hồi trong tình trạng hỏng => tài sản DT-003 đã ở trạng thái Broken (xem phiếu Failed tương ứng).
            new AllocationSeed("DT-003", "Phong Ke toan", "Do Thi Oanh", 200, 35, AssetReturnCondition.Damaged, null, "Vo man hinh, khong khoi dong duoc."),
        };

        foreach (var seed in allocations)
        {
            if (!assetIdByCode.TryGetValue(seed.AssetCode, out var assetId)
                || !departmentIdByName.TryGetValue(seed.DepartmentName, out var departmentId))
            {
                continue;
            }

            var allocatedDate = today.AddDays(-seed.AllocatedDaysAgo);
            DateOnly? returnedDate = seed.ReturnedDaysAgo.HasValue ? today.AddDays(-seed.ReturnedDaysAgo.Value) : null;

            if (await context.AssetAllocations.AnyAsync(x =>
                    x.AssetId == assetId && x.RecipientName == seed.RecipientName))
            {
                continue;
            }

            context.AssetAllocations.Add(new AssetAllocation
            {
                AssetId = assetId,
                DepartmentId = departmentId,
                RecipientName = seed.RecipientName,
                AllocatedDate = allocatedDate,
                ReturnedDate = returnedDate,
                HandoverNote = seed.HandoverNote,
                ReturnCondition = seed.ReturnCondition,
                ReturnNote = seed.ReturnNote,
                Status = returnedDate.HasValue ? AllocationStatus.Returned : AllocationStatus.Allocated,
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private sealed record TicketSeed(
        string AssetCode,
        string IssueDescription,
        TicketStatus Status,
        string? TechnicianEmail,
        int DaysAgo,
        double? ResolutionHours,
        string? Notes);

    private sealed record AllocationSeed(
        string AssetCode,
        string DepartmentName,
        string RecipientName,
        int AllocatedDaysAgo,
        int? ReturnedDaysAgo,
        AssetReturnCondition? ReturnCondition,
        string? HandoverNote,
        string? ReturnNote);

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
