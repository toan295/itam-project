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
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<SoftwareLicense> SoftwareLicenses => Set<SoftwareLicense>();
    public DbSet<AssetSoftwareLicense> AssetSoftwareLicenses => Set<AssetSoftwareLicense>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<AssetAllocation> AssetAllocations => Set<AssetAllocation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BudgetForecast> BudgetForecasts => Set<BudgetForecast>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();

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
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.RefreshToken).HasMaxLength(255);
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

        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
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

            entity.HasOne(e => e.License)
                .WithMany(l => l.AssetSoftwareLicenses)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaintenanceTicket>(entity =>
        {
            entity.Property(e => e.IssueDescription).HasMaxLength(1000);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<byte>();
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.TechnicianId);
            entity.HasIndex(e => e.Status);

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
            entity.HasIndex(e => new { e.Year, e.DepartmentId }).IsUnique();

            entity.HasOne(e => e.Department)
                .WithMany(d => d.BudgetForecasts)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
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
