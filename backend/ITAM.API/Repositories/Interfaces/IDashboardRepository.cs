using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public record AssetGroupCount(int Id, string Name, AssetStatus Status, int Count);
public record PurchaseYearCount(int Year, int Count, int WithTicket);   // Year = 0: thiếu hoặc sai (trống / tương lai) ngày mua
public record LicenseCounts(int Total, int ExpiringSoon, int Expired);
// [ĐÃ ĐÓNG]
// public record BudgetTotals(int ForecastCount, int ReplacementCount, int ExpansionCount, decimal ReplacementBudget, decimal ExpansionBudget, DateTime? LatestGeneratedAt);

// Mọi hàm tổng hợp ở DB (GROUP BY) — không kéo toàn bộ tài sản về bộ nhớ. departmentId = null: toàn hệ thống.
public interface IDashboardRepository
{
    Task<Dictionary<AssetStatus, int>> GetAssetStatusCountsAsync(int? departmentId);               // gồm cả Disposed
    Task<List<AssetGroupCount>> GetCategoryStatusCountsAsync(int? departmentId);                    // không tính Disposed
    Task<List<AssetGroupCount>> GetDepartmentStatusCountsAsync(int? departmentId);                  // không tính Disposed
    Task<List<PurchaseYearCount>> GetPurchaseYearCountsAsync(int? departmentId, DateOnly today);    // không tính Disposed
    Task<Dictionary<TicketStatus, int>> GetTicketStatusCountsAsync(int? departmentId);              // phiếu của tài sản không Disposed
    Task<LicenseCounts> GetLicenseCountsAsync(DateOnly today, int expiringDays);
    // [ĐÃ ĐÓNG]
    // Task<BudgetTotals> GetBudgetTotalsAsync(int year, int? departmentId);
    Task<string?> GetDepartmentNameAsync(int departmentId);
}
