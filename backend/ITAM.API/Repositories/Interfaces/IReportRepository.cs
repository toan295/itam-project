using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public record InventoryFilter(int? DepartmentId, int? CategoryId, AssetStatus? Status, int? PurchaseYear, string? Keyword);

public record InventoryRow(
    string AssetCode, string Name, string CategoryName, string DepartmentName, string? SerialNumber, AssetStatus Status,
    DateOnly? PurchaseDate, DateOnly? WarrantyExpiry, string? Holder);

public record OpenTicketRow(
    int TicketId, string AssetCode, string AssetName, string CategoryName, string DepartmentName, TicketPriority Priority,
    DateTime ReportedDate, string? TechnicianName, string Description);

public record WarrantyRow(
    string AssetCode, string Name, string CategoryName, string DepartmentName, AssetStatus Status, DateOnly? PurchaseDate, DateOnly WarrantyExpiry);

public record LicenseRow(int Id, string SoftwareName, string LicenseKey, DateOnly ExpiryDate, int MaxUsage, int CurrentUsage, string? Notes);

public record LicenseAssignmentRow(int LicenseId, string SoftwareName, string AssetCode, string AssetName, string DepartmentName, DateOnly AssignedDate);

// Các hàm Take(limit + 1): trả tối đa limit + 1 dòng để Service biết đã vượt giới hạn mà không phải đếm riêng (không kéo cả bảng về bộ nhớ).
public interface IReportRepository
{
    Task<List<InventoryRow>> GetInventoryAsync(InventoryFilter filter, int limit);
    Task<List<OpenTicketRow>> GetOpenTicketsAsync(int? departmentId, int? categoryId, int limit);
    Task<List<WarrantyRow>> GetWarrantyExpiringAsync(int? departmentId, int? categoryId, DateOnly from, DateOnly to, int limit);
    Task<List<LicenseRow>> GetLicensesAsync(int limit);
    Task<List<LicenseAssignmentRow>> GetLicenseAssignmentsAsync(int limit);
    Task<string?> GetUserNameAsync(int userId);
    Task<string?> GetDepartmentNameAsync(int departmentId);
    Task<string?> GetCategoryNameAsync(int categoryId);
}
