using ITAM.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetCategoryLifecyclePolicy> AssetCategoryLifecyclePolicies => Set<AssetCategoryLifecyclePolicy>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<SoftwareLicense> SoftwareLicenses => Set<SoftwareLicense>();
    public DbSet<AssetSoftwareLicense> AssetSoftwareLicenses => Set<AssetSoftwareLicense>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<AssetAllocation> AssetAllocations => Set<AssetAllocation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DisposalStatus> DisposalStatuses => Set<DisposalStatus>();
    public DbSet<DisposalRequest> DisposalRequests => Set<DisposalRequest>();
    public DbSet<BudgetForecast> BudgetForecasts => Set<BudgetForecast>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<AssetCategoryReferencePrice> AssetCategoryReferencePrices => Set<AssetCategoryReferencePrice>();
    public DbSet<AccountActivation> AccountActivations => Set<AccountActivation>();
    public DbSet<DepartmentExpansionPlan> DepartmentExpansionPlans => Set<DepartmentExpansionPlan>();
    public DbSet<AssetCategoryDeviceRatio> AssetCategoryDeviceRatios => Set<AssetCategoryDeviceRatio>();
    public DbSet<MaintenanceTicketHistory> MaintenanceTicketHistories => Set<MaintenanceTicketHistory>();

    // Tài sản và phiếu bảo trì mang số phiên bản (optimistic concurrency): mỗi lần bản ghi bị sửa — dù từ màn nào
    // (Tài sản, Bảo trì, Phân bổ, Thanh lý...) — Version tăng 1, để màn đang mở dữ liệu cũ bị từ chối thay vì ghi đè.
    private void BumpVersions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is not (Asset or MaintenanceTicket or AssetCategoryLifecyclePolicy or AssetCategoryReferencePrice
                or AssetCategoryDeviceRatio or DepartmentExpansionPlan))
            {
                continue;
            }

            var version = entry.Property("Version");
            // Bản ghi đọc ở chế độ không tracking rồi ép Modified (AssetRepository.Update) có Version "modified" nhưng
            // giá trị chưa đổi — vẫn phải tăng; chỉ bỏ qua khi code đã tự gán Version khác bản gốc.
            if (Equals(version.CurrentValue, version.OriginalValue))
            {
                version.CurrentValue = (int)version.OriginalValue! + 1;
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleId, e.PermissionCode });
            entity.Property(e => e.PermissionCode).HasMaxLength(80);
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            // Duy nhất chỉ tính trên dòng CHƯA xoá: dòng đã xoá mềm không chiếm chỗ nên tạo lại cùng tên/mã vẫn được.
            entity.Property<string?>("ActiveName").HasMaxLength(100).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Name` END", stored: true);
            entity.HasIndex("ActiveName").IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Xác nhận thông tin tài khoản qua email — mỗi lần gửi một dòng; lần mới nhất quyết định trạng thái.
        modelBuilder.Entity<AccountActivation>(entity =>
        {
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.Property(e => e.EmailTo).HasMaxLength(150);
            entity.Property(e => e.Reason).HasMaxLength(30);
            entity.Property(e => e.SendError).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.RoleId);
            entity.HasIndex(e => e.DepartmentId);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisposalStatus>(entity =>
        {
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property<string?>("ActiveCode").HasMaxLength(50).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Code` END", stored: true);
            entity.HasIndex("ActiveCode").IsUnique();
            // Duy nhất chỉ tính trên dòng CHƯA xoá: dòng đã xoá mềm không chiếm chỗ nên tạo lại cùng tên/mã vẫn được.
            entity.Property<string?>("ActiveName").HasMaxLength(100).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Name` END", stored: true);
            entity.HasIndex("ActiveName").IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);

            // 5 bước chính của luồng — nằm trong migration nên có sẵn ở MỌI môi trường (không chỉ Development).
            entity.HasData(
                new DisposalStatus { Id = 1, Code = "Inspected", Name = "Đã kiểm tra", Description = "Technician đã kiểm tra tài sản", Color = "info", SortOrder = 1, IsSystem = true },
                new DisposalStatus { Id = 2, Code = "Proposed", Name = "Đã đề xuất", Description = "Technician đề xuất thanh lý, chờ Manager duyệt", Color = "warning", SortOrder = 2, IsSystem = true },
                new DisposalStatus { Id = 3, Code = "Approved", Name = "Đã duyệt", Description = "Manager đã duyệt, chờ Admin IT thực hiện thanh lý", Color = "success", SortOrder = 3, IsSystem = true },
                new DisposalStatus { Id = 4, Code = "Rejected", Name = "Từ chối", Description = "Manager từ chối đề xuất", Color = "danger", SortOrder = 4, IsSystem = true },
                new DisposalStatus { Id = 5, Code = "Completed", Name = "Hoàn tất", Description = "Admin IT đã thanh lý, tài sản chuyển sang Đã thanh lý", Color = "slate", SortOrder = 5, IsSystem = true });
        });

        modelBuilder.Entity<DisposalRequest>(entity =>
        {
            entity.Property(e => e.InspectionNote).HasMaxLength(2000);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.DisposalMethod).HasMaxLength(100);
            entity.Property(e => e.ReviewNote).HasMaxLength(1000);
            entity.Property(e => e.CompletionNote).HasMaxLength(1000);
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.ActiveAssetId).IsUnique();
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.SubStatusId);

            entity.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Status).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SubStatus).WithMany().HasForeignKey(e => e.SubStatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InspectedBy).WithMany().HasForeignKey(e => e.InspectedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ProposedBy).WithMany().HasForeignKey(e => e.ProposedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReviewedBy).WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CompletedBy).WithMany().HasForeignKey(e => e.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100);
            // Duy nhất chỉ tính trên dòng CHƯA xoá: dòng đã xoá mềm không chiếm chỗ nên tạo lại cùng tên/mã vẫn được.
            entity.Property<string?>("ActiveName").HasMaxLength(100).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `Name` END", stored: true);
            entity.HasIndex("ActiveName").IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<AssetCategoryLifecyclePolicy>(entity =>
        {
            entity.HasIndex(e => e.CategoryId).IsUnique();
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.Property(e => e.AssetCode).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.Specification).HasMaxLength(500);
            entity.Property(e => e.OperatingSystem).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasIndex(e => e.AssetCode).IsUnique();
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Assets)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.Assets)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SoftwareLicense>(entity =>
        {
            entity.Property(e => e.SoftwareName).HasMaxLength(150);
            entity.Property(e => e.LicenseKey).HasMaxLength(255);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasIndex(e => e.ExpiryDate);
            // Duy nhất chỉ tính trên dòng CHƯA xoá: dòng đã xoá mềm không chiếm chỗ nên tạo lại cùng tên/mã vẫn được.
            entity.Property<string?>("ActiveLicenseKey").HasMaxLength(255).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `LicenseKey` END", stored: true);
            entity.HasIndex("ActiveLicenseKey").IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);

        });

        modelBuilder.Entity<AssetSoftwareLicense>(entity =>
        {
            entity.HasIndex(e => new { e.AssetId, e.LicenseId }).IsUnique();

            entity.HasOne(e => e.Asset)
                .WithMany(a => a.AssetSoftwareLicenses)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (không Cascade): xoá License đang được gán cho tài sản phải bị chặn tường
            // minh ở tầng Service (SoftwareLicenseService.DeleteAsync ném lỗi 409 rõ ràng) thay vì
            // để DB âm thầm xoá luôn lịch sử gán — Admin IT phải Gỡ hết trước khi Xoá, giống hệt quy
            // tắc "không xoá AssetCategory đang được tài sản dùng" đã áp dụng cho module Assets.
            entity.HasOne(e => e.License)
                .WithMany(l => l.AssetSoftwareLicenses)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceTicket>(entity =>
        {
            entity.Property(e => e.IssueDescription).HasMaxLength(1000);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.Property(e => e.Priority).HasConversion<byte>();
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.TechnicianId);
            entity.HasIndex(e => e.ReportedByUserId);
            entity.HasOne(e => e.ReportedBy).WithMany().HasForeignKey(e => e.ReportedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);

            entity.HasOne(e => e.Asset)
                .WithMany(a => a.MaintenanceTickets)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Technician)
                .WithMany(u => u.MaintenanceTickets)
                .HasForeignKey(e => e.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceTicketHistory>(entity =>
        {
            entity.Property(e => e.Action).HasMaxLength(30);
            entity.Property(e => e.FromValue).HasMaxLength(200);
            entity.Property(e => e.ToValue).HasMaxLength(200);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ActorName).HasMaxLength(100);
            entity.HasIndex(e => new { e.TicketId, e.CreatedAt });
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.History)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetAllocation>(entity =>
        {
            entity.Property(e => e.RecipientName).HasMaxLength(100);
            entity.Property(e => e.HandoverNote).HasMaxLength(500);
            entity.Property(e => e.HandoverReason).HasMaxLength(255);
            entity.Property(e => e.HandoverLocation).HasMaxLength(200);
            entity.Property(e => e.HandoverCondition).HasMaxLength(50);
            entity.Property(e => e.RecipientPosition).HasMaxLength(100);
            entity.HasIndex(e => e.ActiveAssetId).IsUnique();
            entity.HasIndex(e => e.EmployeeId);
            entity.Property(e => e.ReturnCondition).HasConversion<byte>();
            entity.Property(e => e.ReturnNote).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.DepartmentId);

            entity.HasOne(e => e.Asset)
                .WithMany(a => a.AssetAllocations)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.AssetAllocations)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.HandedOverBy).WithMany().HasForeignKey(e => e.HandedOverByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReceivedBy).WithMany().HasForeignKey(e => e.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(e => e.EmployeeCode).HasMaxLength(20);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property<string?>("ActiveEmployeeCode").HasMaxLength(20).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN `EmployeeCode` END", stored: true);
            entity.HasIndex("ActiveEmployeeCode").IsUnique();
            // Mỗi tài khoản đăng nhập gắn tối đa một nhân viên (và ngược lại).
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.Property<string?>("ActiveDeptFullName").HasMaxLength(120).HasComputedColumnSql("CASE WHEN `IsDeleted` = 0 THEN CONCAT(`DepartmentId`, '|', `FullName`) END", stored: true);
            entity.HasIndex("ActiveDeptFullName").IsUnique();
            entity.HasIndex(e => e.FullName);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.Outcome).HasMaxLength(20).HasDefaultValue(AuditOutcome.Success);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Detail).HasColumnType("longtext");
            entity.Property(e => e.RequestId).HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.ActorName).HasMaxLength(150);
            entity.Property(e => e.ActorRole).HasMaxLength(50);
            entity.Property(e => e.RowHash).HasMaxLength(64);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityName, e.EntityId });
            entity.HasIndex(e => e.Outcome);

            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetForecast>(entity =>
        {
            entity.Property(e => e.EstimatedBudget).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.BreakdownJson).HasColumnType("longtext");
            entity.Property(e => e.EstimatedExpansionBudget).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Revision).HasDefaultValue(1);
            entity.Property(e => e.InputsHash).HasMaxLength(64);
            entity.Property(e => e.InputsJson).HasColumnType("longtext");
            entity.Property(e => e.ExpansionBreakdownJson).HasColumnType("longtext");
            entity.HasIndex(e => new { e.Year, e.DepartmentId }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.BudgetForecasts)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Module Dự báo ngân sách — đơn giá tham khảo theo loại (VND).
        modelBuilder.Entity<AssetCategoryReferencePrice>(entity =>
        {
            entity.ToTable("AssetCategoryReferencePrices");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Category)
                .WithOne()
                .HasForeignKey<AssetCategoryReferencePrice>(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Module Dự báo ngân sách — kế hoạch mở rộng nhân sự theo (năm, phòng ban).
        modelBuilder.Entity<DepartmentExpansionPlan>(entity =>
        {
            entity.ToTable("DepartmentExpansionPlans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasIndex(e => new { e.Year, e.DepartmentId }).IsUnique();   // mỗi (năm, phòng ban) tối đa 1 dòng

            entity.HasOne(e => e.Department)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);   // không âm thầm xoá kế hoạch khi xoá phòng ban
        });

        // Module Dự báo ngân sách — định mức thiết bị/người theo loại tài sản.
        modelBuilder.Entity<AssetCategoryDeviceRatio>(entity =>
        {
            entity.ToTable("AssetCategoryDeviceRatios");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.DevicesPerPerson).HasColumnType("decimal(6,2)");
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();

            entity.HasOne(e => e.Category)
                .WithOne()
                .HasForeignKey<AssetCategoryDeviceRatio>(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportLog>(entity =>
        {
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.Mode).HasMaxLength(20).HasDefaultValue("skip-errors");
            entity.Property(e => e.FileSha256).HasMaxLength(64);
            entity.Property(e => e.ErrorsJson).HasColumnType("longtext");
            entity.HasIndex(e => e.FileSha256);

            entity.HasOne(e => e.ImportedByUser)
                .WithMany(u => u.ImportLogs)
                .HasForeignKey(e => e.ImportedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
