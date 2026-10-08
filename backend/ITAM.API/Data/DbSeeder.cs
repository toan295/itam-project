using ITAM.API.Helpers;
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
    // tài khoản thật: tài khoản do Admin IT tạo qua UsersController dùng mật khẩu mặc định
    // (cấu hình UserDefaults:DefaultPassword) và nên được người dùng đổi ngay sau lần đăng nhập đầu.
    private const string DemoPassword = "Demo@123";
    private const string InitialDefaultPassword = "Eaims@123";

    public static async Task SeedAsync(AppDbContext context, DefaultPasswordCipher cipher)
    {
        await SeedRolesAsync(context);
        await SeedDepartmentsAsync(context);
        await SeedAssetCategoriesAsync(context);
        await SeedAdminUserAsync(context);
        await SeedDefaultPasswordAsync(context, cipher);

        // Bộ dữ liệu demo mở rộng — phục vụ trình bày/test giao diện đầy đủ chức năng.
        // Ghi chú cho Hoàng Đức Tú: đây là phần thêm mới (không đổi 4 hàm seed nền tảng phía trên),
        // chỉ chạy ở môi trường Development (xem Program.cs), an toàn cho Production.
        await SeedDemoUsersAsync(context);
        await SeedDemoAssetsAsync(context);
        await SeedDemoSoftwareLicensesAsync(context);
        await SeedDemoAssetLicenseAssignmentsAsync(context);
        await SeedDemoMaintenanceTicketsAsync(context);
        await SeedDemoEmployeesAsync(context);
        await SeedDemoAssetAllocationsAsync(context);
        await SeedDemoDisposalRequestsAsync(context);
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
                Name = "Phòng IT",
                Description = "Phòng phụ trách hạ tầng và hỗ trợ công nghệ thông tin."
            },
            new Department
            {
                Name = "Phòng Kế toán",
                Description = "Phòng phụ trách kế toán và tài chính."
            },
            new Department
            {
                Name = "Phòng Nhân sự",
                Description = "Phòng phụ trách nhân sự."
            },
            new Department
            {
                Name = "Phòng Kinh doanh",
                Description = "Phòng phụ trách kinh doanh và chăm sóc khách hàng."
            },
            new Department
            {
                Name = "Phòng Marketing",
                Description = "Phòng phụ trách truyền thông, thương hiệu và sự kiện."
            },
            // Cố ý KHÔNG gán người dùng/tài sản nào — dùng để thử chức năng Xoá phòng ban (UC-04):
            // phòng ban đang có dữ liệu tham chiếu sẽ bị từ chối xoá, phòng ban trống này thì xoá được.
            new Department
            {
                Name = "Phòng Hành chính",
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
            "Máy tính",
            "Màn hình",
            "Máy in",
            "Switch/Router",
            "Điện thoại di động",
            "Máy chiếu",
            "Máy chủ",
            "Máy quét",
            // Cố ý chưa có tài sản nào dùng — để thử chức năng Xoá loại tài sản (UC-04).
            "Phụ kiện"
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
            .SingleAsync(department => department.Name == "Phòng IT");

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

    // Mật khẩu mặc định ban đầu (chỉ Development) để Admin IT cấp tài khoản ngay được; Admin sửa/xoá trên giao diện.
    private static async Task SeedDefaultPasswordAsync(AppDbContext context, DefaultPasswordCipher cipher)
    {
        if (await context.SystemSettings.AnyAsync(s => s.Key == "DefaultPassword"))
        {
            return;
        }

        context.SystemSettings.Add(new SystemSetting { Key = "DefaultPassword", Value = cipher.Protect(InitialDefaultPassword) });
        await context.SaveChangesAsync();
    }

    // Phiếu thanh lý mẫu phủ đủ các bước của luồng (Inspected -> Proposed -> Approved/Rejected -> Completed) và
    // 1 trạng thái phụ do Admin thêm. Phiếu Completed đi cùng tài sản đã Disposed sẵn trong bộ dữ liệu tài sản.
    private static async Task SeedDemoDisposalRequestsAsync(AppDbContext context)
    {
        if (await context.DisposalRequests.AnyAsync())
        {
            return;
        }

        var assetByCode = await context.Assets.ToDictionaryAsync(a => a.AssetCode, a => a.Id);
        var userByEmail = await context.Users.ToDictionaryAsync(u => u.Email, u => u.Id);
        var statusByCode = await context.DisposalStatuses.ToDictionaryAsync(s => s.Code, s => s);

        // Dùng lại trạng thái phụ nếu đã có (vd: ai đó xoá hết phiếu thanh lý rồi chạy lại) — thêm trùng Code sẽ
        // vi phạm unique index và làm ứng dụng không khởi động được.
        if (!statusByCode.TryGetValue("custom-seed-docs", out var waitingDocs))
        {
            waitingDocs = new DisposalStatus
            {
                Code = "custom-seed-docs",
                Name = "Chờ chứng từ",
                Description = "Đang chờ biên bản/chứng từ thanh lý",
                Color = "warning",
                SortOrder = 10,
                IsSystem = false,
            };
            context.DisposalStatuses.Add(waitingDocs);
            await context.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        var requests = new[]
        {
            // code, status, tech, manager, admin, daysAgo, subStatus, reviewNote
            new DisposalSeed("MI-003", DisposalStatusCodes.Inspected, "tech1@eaims.local", null, null, 2, null, null, "Brother HL-L2321D hỏng bộ phận kéo giấy, chi phí sửa cao."),
            new DisposalSeed("PC-005", DisposalStatusCodes.Proposed, "tech2@eaims.local", null, null, 5, null, null, "Mainboard hỏng, không khởi động, hết bảo hành."),
            new DisposalSeed("DT-003", DisposalStatusCodes.Approved, "tech3@eaims.local", "manager.ketoan@eaims.local", null, 9, waitingDocs, "Đồng ý thanh lý, chuyển Admin IT thực hiện.", "Vỡ màn hình và hỏng nguồn, không thay thế được linh kiện."),
            new DisposalSeed("PC-003", DisposalStatusCodes.Rejected, "tech1@eaims.local", "manager.nhansu@eaims.local", null, 14, null, "Máy vẫn dùng tốt, chưa cần thanh lý.", "Máy cũ, chạy chậm."),
            new DisposalSeed("MI-002", DisposalStatusCodes.Completed, "tech2@eaims.local", "manager.it@eaims.local", "admin@eaims.local", 30, null, "Đồng ý.", "Máy in đã hết tuổi thọ sử dụng."),
        };

        foreach (var seed in requests)
        {
            if (!assetByCode.TryGetValue(seed.AssetCode, out var assetId)) continue;

            var created = now.AddDays(-seed.DaysAgo);
            var request = new DisposalRequest
            {
                AssetId = assetId,
                StatusId = statusByCode[seed.StatusCode].Id,
                SubStatusId = seed.SubStatus?.Id,
                InspectionNote = seed.InspectionNote,
                InspectedByUserId = userByEmail[seed.TechEmail],
                InspectedAt = created,
                CreatedAt = created,
            };

            if (seed.StatusCode != DisposalStatusCodes.Inspected)
            {
                request.Reason = "Hết khấu hao / chi phí sửa chữa vượt giá trị tài sản.";
                request.DisposalMethod = "Thanh lý phế liệu";
                request.ProposedByUserId = userByEmail[seed.TechEmail];
                request.ProposedAt = created.AddHours(4);
            }

            if (seed.ManagerEmail is not null)
            {
                request.ReviewNote = seed.ReviewNote;
                request.ReviewedByUserId = userByEmail[seed.ManagerEmail];
                request.ReviewedAt = created.AddDays(1);
            }

            if (seed.AdminEmail is not null)
            {
                request.CompletionNote = "Đã bàn giao đơn vị thanh lý.";
                request.CompletedByUserId = userByEmail[seed.AdminEmail];
                request.CompletedAt = created.AddDays(2);
            }

            context.DisposalRequests.Add(request);
        }

        await context.SaveChangesAsync();
    }

    private sealed record DisposalSeed(
        string AssetCode, string StatusCode, string TechEmail, string? ManagerEmail, string? AdminEmail,
        int DaysAgo, DisposalStatus? SubStatus, string? ReviewNote, string InspectionNote);

    private static async Task SeedDemoUsersAsync(AppDbContext context)
    {
        var roleIdByName = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);

        var demoUsers = new[]
        {
            new { FullName = "Nguyễn Văn Lý", Email = "manager.it@eaims.local", Role = "Manager", Department = "Phòng IT", Active = true },
            new { FullName = "Trần Thị Trương", Email = "manager.ketoan@eaims.local", Role = "Manager", Department = "Phòng Kế toán", Active = true },
            new { FullName = "Hoàng Văn Doanh", Email = "manager.kinhdoanh@eaims.local", Role = "Manager", Department = "Phòng Kinh doanh", Active = true },
            new { FullName = "Lê Văn Thuật", Email = "tech1@eaims.local", Role = "Technician", Department = "Phòng IT", Active = true },
            new { FullName = "Phạm Thị Trợ", Email = "tech2@eaims.local", Role = "Technician", Department = "Phòng IT", Active = true },
            new { FullName = "Ngô Văn Hiếu", Email = "tech3@eaims.local", Role = "Technician", Department = "Phòng IT", Active = true },

            // --- Bộ mở rộng: Manager cho từng phòng ban + Admin thứ 2 + 1 tài khoản bị khoá ---
            // Technician: đúng 3 kỹ thuật viên hoạt động (tech1..3), cùng phục vụ MỌI phòng ban — DepartmentId chỉ là phòng ban quản lý hồ sơ.
            new { FullName = "Bùi Quốc Bảo", Email = "admin.phu@eaims.local", Role = "Admin IT", Department = "Phòng IT", Active = true },
            new { FullName = "Vũ Thị Lan", Email = "manager.nhansu@eaims.local", Role = "Manager", Department = "Phòng Nhân sự", Active = true },
            new { FullName = "Đặng Minh Quân", Email = "manager.marketing@eaims.local", Role = "Manager", Department = "Phòng Marketing", Active = true },
            // Tài khoản đã bị khoá (IsActive = false) — minh hoạ UC-01 E2 và chức năng khoá/mở khoá của Admin (UC-03).
            new { FullName = "Cao Văn Tùng", Email = "nghi.viec@eaims.local", Role = "Technician", Department = "Phòng IT", Active = false },
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
            // --- Bộ gốc (Máy tính / Màn hình / Máy in / Switch) ---
            new AssetSeed("PC-001", "Dell OptiPlex 7090", "Máy tính", "Phòng IT", AssetStatus.InUse, "SN-PC001", null, "Windows 11 Pro", new DateOnly(2024, 3, 10), new DateOnly(2027, 3, 10)),
            new AssetSeed("PC-002", "Dell Latitude 5420", "Máy tính", "Phòng Kế toán", AssetStatus.InUse, "SN-PC002", null, "Windows 11 Pro", new DateOnly(2023, 6, 15), new DateOnly(2026, 6, 15)),
            new AssetSeed("PC-003", "HP EliteBook 840", "Máy tính", "Phòng Nhân sự", AssetStatus.InUse, "SN-PC003", null, "Windows 10 Pro", new DateOnly(2021, 1, 20), new DateOnly(2024, 1, 20)),
            new AssetSeed("PC-004", "Lenovo ThinkCentre M70q", "Máy tính", "Phòng IT", AssetStatus.Maintenance, null, null, "Windows 11 Pro", new DateOnly(2023, 11, 1), new DateOnly(2026, 11, 1)),
            new AssetSeed("PC-005", "Dell Vostro 3400", "Máy tính", "Phòng Kế toán", AssetStatus.Broken, null, null, "Windows 10 Home", new DateOnly(2020, 5, 12), new DateOnly(2023, 5, 12)),
            new AssetSeed("MH-001", "Màn hình Dell UltraSharp 24 inch", "Màn hình", "Phòng IT", AssetStatus.InUse, "SN-MH001", null, null, new DateOnly(2024, 1, 15), new DateOnly(2027, 1, 15)),
            new AssetSeed("MH-002", "Màn hình Samsung Odyssey", "Màn hình", "Phòng Nhân sự", AssetStatus.InUse, "SN-MH002", null, null, new DateOnly(2022, 7, 1), new DateOnly(2025, 7, 1)),
            new AssetSeed("MI-001", "Máy in Canon LBP2900", "Máy in", "Phòng Kế toán", AssetStatus.InUse, "SN-MI001", null, null, new DateOnly(2023, 2, 20), new DateOnly(2025, 2, 20)),
            new AssetSeed("MI-002", "Máy in HP LaserJet Pro", "Máy in", "Phòng IT", AssetStatus.Disposed, "SN-MI002", null, null, new DateOnly(2019, 8, 1), new DateOnly(2021, 8, 1)),
            new AssetSeed("SW-001", "Cisco Catalyst 2960", "Switch/Router", "Phòng IT", AssetStatus.InUse, "SN-SW001", null, null, new DateOnly(2022, 9, 1), new DateOnly(2025, 9, 1)),

            // --- Bộ mở rộng: thêm phòng ban/danh mục mới, đa dạng tình trạng bảo hành ---
            new AssetSeed("PC-006", "Dell Latitude 5430", "Máy tính", "Phòng Kinh doanh", AssetStatus.InUse, "SN-PC006", "RAM 16GB, SSD 512GB", "Windows 11 Pro", new DateOnly(2025, 1, 10), new DateOnly(2028, 1, 10)),
            new AssetSeed("PC-007", "MacBook Pro 14 M3", "Máy tính", "Phòng Kinh doanh", AssetStatus.InUse, "SN-PC007", "RAM 18GB, SSD 1TB", "macOS Sonoma", new DateOnly(2024, 9, 1), new DateOnly(2026, 9, 1)),
            new AssetSeed("LT-001", "Dell XPS 13", "Máy tính", "Phòng Nhân sự", AssetStatus.Maintenance, "SN-LT001", null, "Windows 11 Home", new DateOnly(2023, 4, 1), new DateOnly(2026, 4, 1)),
            new AssetSeed("DT-001", "iPhone 13", "Điện thoại di động", "Phòng Kinh doanh", AssetStatus.InUse, "SN-DT001", "128GB, màu xanh", "iOS 17", new DateOnly(2024, 11, 20), new DateOnly(2025, 11, 20)),
            new AssetSeed("DT-002", "Samsung Galaxy S23", "Điện thoại di động", "Phòng IT", AssetStatus.InUse, "SN-DT002", "256GB", "Android 14", new DateOnly(2025, 8, 1), new DateOnly(2027, 8, 1)),
            new AssetSeed("DT-003", "Xiaomi Redmi Note 12", "Điện thoại di động", "Phòng Kế toán", AssetStatus.Broken, "SN-DT003", null, "Android 13", new DateOnly(2022, 3, 15), new DateOnly(2024, 3, 15)),
            new AssetSeed("MC-001", "Máy chiếu Epson EB-X05", "Máy chiếu", "Phòng Nhân sự", AssetStatus.InUse, "SN-MC001", "3300 Lumens", null, new DateOnly(2023, 10, 5), new DateOnly(2026, 10, 5)),
            new AssetSeed("MC-002", "Máy chiếu BenQ MW612", "Máy chiếu", "Phòng IT", AssetStatus.Disposed, "SN-MC002", null, null, new DateOnly(2018, 6, 1), new DateOnly(2020, 6, 1)),
            new AssetSeed("SW-002", "TP-Link Switch 24-port", "Switch/Router", "Phòng Kế toán", AssetStatus.InUse, "SN-SW002", "24 cổng Gigabit", null, new DateOnly(2024, 5, 20), new DateOnly(2027, 5, 20)),
            // Không có ngày mua/bảo hành — mô phỏng tài sản cũ/được bàn giao không rõ nguồn gốc mua sắm.
            new AssetSeed("MH-003", "Màn hình LG UltraGear", "Màn hình", "Phòng Kinh doanh", AssetStatus.InUse, "SN-MH003", null, null, null, null),

            // --- Bộ mở rộng 2: đủ 5 phòng ban có tài sản, thêm loại Máy chủ / Máy quét, đa dạng trạng thái/bảo hành ---
            // Phòng IT
            new AssetSeed("SV-001", "Dell PowerEdge R750", "Máy chủ", "Phòng IT", AssetStatus.InUse, "SN-SV001", "2x Xeon Silver, RAM 128GB, RAID10 4TB", "Windows Server 2022", today.AddDays(-400), today.AddDays(700)),
            new AssetSeed("SV-002", "HPE ProLiant DL380 Gen10", "Máy chủ", "Phòng IT", AssetStatus.InUse, "SN-SV002", "Xeon Gold, RAM 64GB, SSD 2TB", "Ubuntu Server 22.04", new DateOnly(2022, 4, 10), new DateOnly(2025, 4, 10)),
            new AssetSeed("SV-003", "Dell PowerEdge T350", "Máy chủ", "Phòng IT", AssetStatus.InUse, "SN-SV003", "Xeon E-2334, RAM 32GB", "Windows Server 2019", new DateOnly(2023, 8, 20), new DateOnly(2026, 8, 20)),
            new AssetSeed("PC-008", "Lenovo ThinkPad T14", "Máy tính", "Phòng IT", AssetStatus.InUse, "SN-PC008", "Ryzen 7, RAM 16GB, SSD 512GB", "Windows 11 Pro", today.AddDays(-90), today.AddDays(1000)),
            // Bảo hành sắp hết trong 20 ngày — minh hoạ lọc "còn bảo hành" ở gần mốc hết hạn.
            new AssetSeed("MH-004", "Màn hình HP E24 G5", "Màn hình", "Phòng IT", AssetStatus.InUse, "SN-MH004", "24 inch FHD IPS", null, today.AddDays(-1075), today.AddDays(20)),
            new AssetSeed("SW-003", "Ubiquiti UniFi Switch 48", "Switch/Router", "Phòng IT", AssetStatus.InUse, "SN-SW003", "48 cổng PoE+", null, new DateOnly(2024, 2, 5), new DateOnly(2027, 2, 5)),
            new AssetSeed("MI-003", "Máy in Brother HL-L2321D", "Máy in", "Phòng IT", AssetStatus.Broken, "SN-MI003", null, null, new DateOnly(2022, 10, 1), new DateOnly(2024, 10, 1)),
            // Phòng Kế toán
            new AssetSeed("PC-009", "HP ProDesk 400 G7", "Máy tính", "Phòng Kế toán", AssetStatus.InUse, "SN-PC009", "Core i5, RAM 8GB, SSD 256GB", "Windows 10 Pro", new DateOnly(2022, 5, 5), new DateOnly(2025, 5, 5)),
            new AssetSeed("PC-010", "Dell OptiPlex 3080", "Máy tính", "Phòng Kế toán", AssetStatus.InUse, "SN-PC010", "Core i5, RAM 8GB, HDD 1TB", "Windows 10 Pro", new DateOnly(2021, 11, 15), new DateOnly(2024, 11, 15)),
            new AssetSeed("MH-005", "Màn hình Dell P2422H", "Màn hình", "Phòng Kế toán", AssetStatus.InUse, "SN-MH005", "24 inch FHD", null, new DateOnly(2024, 6, 1), new DateOnly(2027, 6, 1)),
            new AssetSeed("SC-001", "Máy quét Fujitsu fi-7160", "Máy quét", "Phòng Kế toán", AssetStatus.InUse, "SN-SC001", "60 trang/phút, ADF 80 tờ", null, new DateOnly(2023, 3, 12), new DateOnly(2026, 3, 12)),
            new AssetSeed("MI-004", "Máy in Epson L3250", "Máy in", "Phòng Kế toán", AssetStatus.InUse, "SN-MI004", "In phun màu, WiFi", null, today.AddDays(-200), today.AddDays(530)),
            // Phòng Nhân sự
            new AssetSeed("PC-011", "Acer Veriton X2690G", "Máy tính", "Phòng Nhân sự", AssetStatus.InUse, "SN-PC011", "Core i5, RAM 8GB, SSD 256GB", "Windows 11 Pro", new DateOnly(2024, 4, 22), new DateOnly(2027, 4, 22)),
            new AssetSeed("MH-006", "Màn hình AOC 24B1H", "Màn hình", "Phòng Nhân sự", AssetStatus.InUse, "SN-MH006", "24 inch FHD VA", null, new DateOnly(2023, 1, 9), new DateOnly(2026, 1, 9)),
            new AssetSeed("SC-002", "Máy quét Canon DR-C225", "Máy quét", "Phòng Nhân sự", AssetStatus.InUse, "SN-SC002", "25 trang/phút", null, new DateOnly(2022, 12, 1), new DateOnly(2025, 12, 1)),
            new AssetSeed("MI-005", "Máy in Brother HL-1110 (cũ)", "Máy in", "Phòng Nhân sự", AssetStatus.Disposed, "SN-MI005", null, null, new DateOnly(2017, 3, 1), new DateOnly(2019, 3, 1)),
            // Phòng Kinh doanh
            new AssetSeed("LT-002", "MacBook Air M2", "Máy tính", "Phòng Kinh doanh", AssetStatus.InUse, "SN-LT002", "RAM 16GB, SSD 512GB", "macOS Sonoma", new DateOnly(2024, 7, 3), new DateOnly(2026, 7, 3)),
            new AssetSeed("LT-003", "Lenovo IdeaPad 5 Pro", "Máy tính", "Phòng Kinh doanh", AssetStatus.InUse, "SN-LT003", "Ryzen 5, RAM 16GB, SSD 512GB", "Windows 11 Home", today.AddDays(-60), today.AddDays(670)),
            new AssetSeed("DT-005", "Samsung Galaxy A54", "Điện thoại di động", "Phòng Kinh doanh", AssetStatus.InUse, "SN-DT005", "128GB", "Android 14", new DateOnly(2025, 3, 18), new DateOnly(2027, 3, 18)),
            new AssetSeed("MC-003", "Máy chiếu ViewSonic PA503S", "Máy chiếu", "Phòng Kinh doanh", AssetStatus.InUse, "SN-MC003", "3800 Lumens", null, new DateOnly(2023, 9, 14), new DateOnly(2026, 9, 14)),
            // Phòng Marketing
            new AssetSeed("PC-012", "iMac 24 inch M3", "Máy tính", "Phòng Marketing", AssetStatus.InUse, "SN-PC012", "RAM 16GB, SSD 512GB", "macOS Sonoma", today.AddDays(-100), today.AddDays(965)),
            new AssetSeed("LT-004", "MacBook Pro 16 M3 Pro", "Máy tính", "Phòng Marketing", AssetStatus.InUse, "SN-LT004", "RAM 36GB, SSD 1TB", "macOS Sonoma", today.AddDays(-150), today.AddDays(915)),
            new AssetSeed("MH-007", "Màn hình LG 27UL500", "Màn hình", "Phòng Marketing", AssetStatus.InUse, "SN-MH007", "27 inch 4K", null, new DateOnly(2024, 3, 8), new DateOnly(2027, 3, 8)),
            new AssetSeed("DT-007", "Samsung Galaxy S24", "Điện thoại di động", "Phòng Marketing", AssetStatus.InUse, "SN-DT007", "256GB", "Android 14", today.AddDays(-120), today.AddDays(610)),
            new AssetSeed("MC-004", "Máy chiếu Optoma HD146X", "Máy chiếu", "Phòng Marketing", AssetStatus.InUse, "SN-MC004", "3600 Lumens Full HD", null, new DateOnly(2023, 5, 25), new DateOnly(2026, 5, 25)),
            new AssetSeed("MI-006", "Máy in màu Canon G3020", "Máy in", "Phòng Marketing", AssetStatus.InUse, "SN-MI006", "In phun màu, WiFi", null, new DateOnly(2024, 10, 10), new DateOnly(2026, 10, 10)),
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
            new LicenseSeed("AutoCAD 2026", "ACAD-2026-0008", new DateOnly(2027, 5, 1), 5, "Chỉ cấp cho phòng ban có nhu cầu thiết kế."),

            // --- Bộ mở rộng (hạn dùng tính theo ngày hiện tại để luôn minh hoạ đúng các trạng thái cảnh báo) ---
            new LicenseSeed("Visual Studio Enterprise 2022", "VS-ENT-2022-0009", new DateOnly(2027, 3, 1), 6, null),
            new LicenseSeed("Microsoft Office LTSC 2021", "OFFICE-LTSC-0010", new DateOnly(2028, 6, 30), 25, "Bản quyền vĩnh viễn theo gói 3 năm."),
            // Sắp hết hạn trong 15 ngày.
            new LicenseSeed("Veeam Backup & Replication", "VEEAM-BR-0011", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15), 4, "Máy chủ backup."),
            // Đã hết hạn 10 ngày trước.
            new LicenseSeed("FortiClient VPN", "FORTI-VPN-0012", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10), 10, "Cần gia hạn."),
            // Dùng hết hạn mức (3/3) — minh hoạ UC-09 E1 (gán thêm sẽ bị từ chối).
            new LicenseSeed("Canva Pro", "CANVA-PRO-0013", new DateOnly(2027, 1, 15), 3, "Dành cho phòng Marketing."),
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
            new TicketSeed("MI-004", "Máy in bị kẹt giấy liên tục, không in được.", TicketStatus.Pending, null, 2, null, null, TicketPriority.High),
            new TicketSeed("PC-009", "Máy khởi động chậm, bị treo khi mở file Excel lớn.", TicketStatus.Pending, null, 1, null, null),
            new TicketSeed("MH-006", "Màn hình bị sọc dọc, mất tín hiệu HDMI thỉnh thoảng.", TicketStatus.Pending, null, 3, null, null, TicketPriority.Low),
            new TicketSeed("LT-002", "Pin chai, chỉ dùng được khoảng 30 phút.", TicketStatus.Pending, null, 5, null, null, TicketPriority.Low),

            // --- Pending, đã gán ---
            new TicketSeed("SV-002", "Quạt tản nhiệt kêu to, nhiệt độ CPU cao bất thường.", TicketStatus.Pending, "tech1@eaims.local", 4, null, null, TicketPriority.Urgent),
            new TicketSeed("PC-004", "Không lên nguồn sau khi mất điện đột ngột.", TicketStatus.Pending, "tech2@eaims.local", 6, null, null, TicketPriority.High),
            new TicketSeed("LT-001", "Thay bàn phím và kiểm tra tình trạng pin.", TicketStatus.Pending, "tech2@eaims.local", 8, null, null),
            new TicketSeed("DT-005", "Màn hình cảm ứng bị loạn cảm ứng ở góc dưới.", TicketStatus.Pending, "tech3@eaims.local", 2, null, null, TicketPriority.Normal),
            // Kỹ thuật viên phòng IT nhận phiếu của phòng Nhân sự (Admin/Manager được gán khác phòng ban).
            new TicketSeed("PC-011", "Cài lại hệ điều hành và cập nhật driver.", TicketStatus.Pending, "tech1@eaims.local", 1, null, null, TicketPriority.Low),

            // --- Resolved (tài sản đang InUse) ---
            new TicketSeed("PC-001", "Cập nhật BIOS và thay pin CMOS.", TicketStatus.Resolved, "tech1@eaims.local", 40, 6, "Đã thay pin CMOS, cập nhật BIOS bản mới nhất."),
            new TicketSeed("PC-002", "Cài lại Windows và sao lưu dữ liệu kế toán.", TicketStatus.Resolved, "tech3@eaims.local", 25, 30, "Đã sao lưu dữ liệu, cài lại Windows 11 và cài phần mềm kế toán."),
            new TicketSeed("MH-001", "Màn hình bị nhấp nháy khi chạy đồ họa.", TicketStatus.Resolved, "tech2@eaims.local", 12, 3.5, "Đổi cáp DisplayPort, hết nhấp nháy."),
            new TicketSeed("MI-001", "Thay drum và vệ sinh máy in.", TicketStatus.Resolved, "tech3@eaims.local", 55, 26, "Đã thay drum mới."),
            new TicketSeed("PC-006", "Máy chạy chậm, nâng cấp RAM từ 8GB lên 16GB.", TicketStatus.Resolved, "tech3@eaims.local", 70, 48, "Đã nâng cấp RAM."),
            // Phiếu của năm trước — để thống kê "số lần bảo trì theo tài sản-năm" (UC-13) có nhiều năm.
            new TicketSeed("SW-001", "Cập nhật firmware switch lên bản mới.", TicketStatus.Resolved, "tech1@eaims.local", 400, 5, "Cập nhật firmware thành công."),
            new TicketSeed("PC-001", "Thay quạt tản nhiệt CPU bị kém.", TicketStatus.Resolved, "tech1@eaims.local", 420, 8, "Đã thay quạt mới."),
            new TicketSeed("PC-003", "Thay ổ cứng SSD, cài lại hệ điều hành.", TicketStatus.Resolved, "tech2@eaims.local", 380, 20, "Đã thay SSD 256GB."),

            // --- Failed (tài sản đang Broken) ---
            new TicketSeed("PC-005", "Mainboard hỏng, không thể khởi động.", TicketStatus.Failed, "tech3@eaims.local", 20, 72, "Chi phí sửa vượt giá trị máy, đề xuất thanh lý.", TicketPriority.High),
            new TicketSeed("DT-003", "Vỡ màn hình và hỏng nguồn.", TicketStatus.Failed, "tech1@eaims.local", 33, 50, "Không thể thay thế linh kiện."),
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
                Priority = seed.Priority,
            });
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        // Quy tắc nghiệp vụ: tài sản đang có phiếu bảo trì chờ xử lý thì ở trạng thái "Bảo trì".
        var pendingAssetIds = await context.MaintenanceTickets
            .Where(t => t.Status == TicketStatus.Pending).Select(t => t.AssetId).Distinct().ToListAsync();
        foreach (var asset in await context.Assets
                     .Where(a => pendingAssetIds.Contains(a.Id) && a.Status != AssetStatus.Disposed && a.Status != AssetStatus.Maintenance)
                     .ToListAsync())
        {
            asset.Status = AssetStatus.Maintenance;
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
    // Danh mục nhân viên nhận tài sản (người nhận khi phân bổ chọn từ đây, không gõ tay). Có kèm vài nhân viên
    // chưa nhận tài sản và 1 người đã ngừng hoạt động để minh hoạ lọc/tìm kiếm.
    private static async Task SeedDemoEmployeesAsync(AppDbContext context)
    {
        if (await context.Employees.AnyAsync())
        {
            return;
        }

        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);

        var roster = new (string Name, string Department, string Position, bool Active)[]
        {
            ("Nguyễn Văn An", "Phòng IT", "Chuyên viên hạ tầng", true),
            ("Trần Minh Tuấn", "Phòng IT", "Trưởng nhóm vận hành", true),
            ("Võ Văn Khoa", "Phòng IT", "Kỹ sư hệ thống", true),
            ("Cao Thị Liên", "Phòng IT", "Chuyên viên hỗ trợ người dùng", true),
            ("Lê Quang Huy", "Phòng IT", "Nhân viên IT", true),
            ("Nguyễn Văn Hậu", "Phòng IT", "Nhân viên IT", false),
            ("Lê Thị Hoa", "Phòng Kế toán", "Kế toán tổng hợp", true),
            ("Mai Văn Nghĩa", "Phòng Kế toán", "Kế toán thanh toán", true),
            ("Đỗ Thị Oanh", "Phòng Kế toán", "Kế toán thuế", true),
            ("Hồ Thị Thu", "Phòng Kế toán", "Thủ quỹ", true),
            ("Trần Văn Bình", "Phòng Nhân sự", "Chuyên viên tuyển dụng", true),
            ("Phạm Thị Cúc", "Phòng Nhân sự", "Chuyên viên C&B", true),
            ("Nguyễn Thị Hương", "Phòng Nhân sự", "Trưởng phòng nhân sự", true),
            ("Hoàng Văn Đức", "Phòng Kinh doanh", "Nhân viên kinh doanh", true),
            ("Đặng Thị Em", "Phòng Kinh doanh", "Nhân viên kinh doanh", true),
            ("Vũ Văn Phúc", "Phòng Kinh doanh", "Trưởng nhóm kinh doanh", true),
            ("Bùi Văn Long", "Phòng Kinh doanh", "Nhân viên kinh doanh", true),
            ("Lý Thị Kim", "Phòng Kinh doanh", "Nhân viên kinh doanh", true),
            ("Nguyễn Thị Phương", "Phòng Marketing", "Chuyên viên thiết kế", true),
            ("Phạm Thu Hà", "Phòng Marketing", "Chuyên viên sự kiện", true),
            ("Đinh Công Minh", "Phòng Marketing", "Chuyên viên truyền thông", true),
        };

        foreach (var e in roster)
        {
            if (!departmentIdByName.TryGetValue(e.Department, out var departmentId)) continue;
            context.Employees.Add(new Employee
            {
                EmployeeCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                FullName = e.Name,
                DepartmentId = departmentId,
                Position = e.Position,
                IsActive = e.Active,
            });
        }

        await context.SaveChangesAsync();

        // Mã nhân viên theo Id (NV0001...), cùng quy tắc EmployeeService.
        foreach (var employee in await context.Employees.ToListAsync())
        {
            employee.EmployeeCode = $"NV{employee.Id:D4}";
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDemoAssetAllocationsAsync(AppDbContext context)
    {
        var assetIdByCode = await context.Assets.ToDictionaryAsync(a => a.AssetCode, a => a.Id);
        var departmentIdByName = await context.Departments.ToDictionaryAsync(d => d.Name, d => d.Id);
        var employeeIdByKey = await context.Employees.ToDictionaryAsync(e => (e.DepartmentId, e.FullName), e => e.Id);
        var adminId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).FirstOrDefaultAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var allocations = new[]
        {
            // --- Đang phân bổ (chưa thu hồi) ---
            new AllocationSeed("PC-001", "Phòng IT", "Nguyễn Văn An", 200, null, null, "Bàn giao kèm chuột, bàn phím.", null),
            new AllocationSeed("PC-002", "Phòng Kế toán", "Lê Thị Hoa", 300, null, null, "Bàn giao kèm túi chống sốc.", null),
            new AllocationSeed("PC-003", "Phòng Nhân sự", "Trần Văn Bình", 500, null, null, null, null),
            new AllocationSeed("MH-002", "Phòng Nhân sự", "Phạm Thị Cúc", 150, null, null, null, null),
            new AllocationSeed("PC-006", "Phòng Kinh doanh", "Hoàng Văn Đức", 100, null, null, "Bàn giao kèm sạc và balo.", null),
            new AllocationSeed("PC-007", "Phòng Kinh doanh", "Đặng Thị Em", 90, null, null, null, null),
            new AllocationSeed("LT-003", "Phòng Kinh doanh", "Vũ Văn Phúc", 50, null, null, "Bàn giao kèm sạc.", null),
            new AllocationSeed("PC-012", "Phòng Marketing", "Nguyễn Thị Phương", 30, null, null, null, null),
            new AllocationSeed("SV-001", "Phòng IT", "Trần Minh Tuấn", 20, null, null, "Máy chủ chạy hệ thống nội bộ.", null),
            // Tài sản của phòng Kinh doanh nhưng cho phòng Marketing mượn dài hạn.
            new AllocationSeed("MC-003", "Phòng Marketing", "Phạm Thu Hà", 10, null, null, "Mượn cho chương trình ra mắt sản phẩm.", null),
            // DT-001: đã thu hồi 1 lần (Good) rồi được phân bổ lại cho người khác.
            new AllocationSeed("DT-001", "Phòng Kinh doanh", "Bùi Văn Long", 140, null, null, null, null),

            // --- Đã thu hồi ---
            new AllocationSeed("DT-001", "Phòng Kinh doanh", "Lý Thị Kim", 300, 150, AssetReturnCondition.Good, null, "Nhân viên nghỉ việc, máy hoạt động tốt."),
            new AllocationSeed("PC-008", "Phòng IT", "Võ Văn Khoa", 120, 30, AssetReturnCondition.Good, null, "Thu hồi để cấp phát cho nhân viên mới."),
            new AllocationSeed("MH-004", "Phòng IT", "Cao Thị Liên", 250, 60, AssetReturnCondition.Good, null, "Đổi màn hình lớn hơn."),
            new AllocationSeed("PC-010", "Phòng Kế toán", "Mai Văn Nghĩa", 400, 90, AssetReturnCondition.Good, null, null),
            // Thu hồi trong tình trạng hỏng => tài sản DT-003 đã ở trạng thái Broken (xem phiếu Failed tương ứng).
            new AllocationSeed("DT-003", "Phòng Kế toán", "Đỗ Thị Oanh", 200, 35, AssetReturnCondition.Damaged, null, "Vỡ màn hình, không khởi động được."),
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
                EmployeeId = employeeIdByKey.TryGetValue((departmentId, seed.RecipientName), out var employeeId) ? employeeId : null,
                RecipientName = seed.RecipientName,
                AllocatedDate = allocatedDate,
                ReturnedDate = returnedDate,
                HandoverNote = seed.HandoverNote,
                HandoverReason = "Cấp phát tài sản phục vụ công việc.",
                HandoverLocation = "Văn phòng công ty",
                HandoverCondition = "Tốt",
                HandedOverByUserId = adminId == 0 ? null : adminId,
                ReceivedByUserId = returnedDate.HasValue && adminId != 0 ? adminId : null,
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
        string? Notes,
        TicketPriority Priority = TicketPriority.Normal);

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
