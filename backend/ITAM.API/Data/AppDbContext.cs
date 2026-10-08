using ITAM.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
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
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DisposalStatus> DisposalStatuses => Set<DisposalStatus>();
    public DbSet<DisposalRequest> DisposalRequests => Set<DisposalRequest>();
    public DbSet<BudgetForecast> BudgetForecasts => Set<BudgetForecast>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<AssetCategoryReferencePrice> AssetCategoryReferencePrices => Set<AssetCategoryReferencePrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
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

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<DisposalStatus>(entity =>
        {
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name).IsUnique();

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
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<AssetCategoryLifecyclePolicy>(entity =>
        {
            entity.HasIndex(e => e.CategoryId).IsUnique();

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
            entity.HasIndex(e => e.LicenseKey).IsUnique();
            entity.HasIndex(e => e.ExpiryDate);
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
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.Property(e => e.Priority).HasConversion<byte>();
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.TechnicianId);
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

        modelBuilder.Entity<AssetAllocation>(entity =>
        {
            entity.Property(e => e.RecipientName).HasMaxLength(100);
            entity.Property(e => e.HandoverNote).HasMaxLength(500);
            entity.Property(e => e.HandoverReason).HasMaxLength(255);
            entity.Property(e => e.HandoverLocation).HasMaxLength(200);
            entity.Property(e => e.HandoverCondition).HasMaxLength(50);
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
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            entity.HasIndex(e => new { e.DepartmentId, e.FullName }).IsUnique();
            entity.HasIndex(e => e.FullName);

            entity.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);

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
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Category)
                .WithOne()
                .HasForeignKey<AssetCategoryReferencePrice>(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportLog>(entity =>
        {
            entity.Property(e => e.FileName).HasMaxLength(255);

            entity.HasOne(e => e.ImportedByUser)
                .WithMany(u => u.ImportLogs)
                .HasForeignKey(e => e.ImportedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
