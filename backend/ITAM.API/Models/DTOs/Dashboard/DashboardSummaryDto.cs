using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Models.DTOs.Dashboard;

// Mỗi widget độc lập: một widget lỗi chỉ báo "error" cho riêng nó, không làm hỏng cả Dashboard (UC-18, luồng ngoại lệ).
// Status: "ok" | "error" (không tải được) | "hidden" (vai trò không có quyền xem dữ liệu này).
public class DashboardWidget<T>
{
    public string Status { get; set; } = "ok";
    public T? Data { get; set; }
    public string? Message { get; set; }
}

public class DashboardScopeDto
{
    public int? DepartmentId { get; set; }          // null = toàn hệ thống
    public string? DepartmentName { get; set; }
    public bool IsLocked { get; set; }               // true: người xem bị giới hạn ở phòng ban của mình (Manager...)
}

public class DashboardSummaryDto
{
    public DateTime GeneratedAt { get; set; }        // UTC: lúc số liệu được tính (có thể là bản cache tối đa CacheSeconds trước)
    public bool FromCache { get; set; }
    public int CacheSeconds { get; set; }
    public int Year { get; set; }                    // năm dùng cho widget ngân sách
    public DashboardScopeDto Scope { get; set; } = new();
    public DashboardWidget<DashboardOverviewDto> Overview { get; set; } = new();
    public DashboardWidget<List<StatusCountDto>> AssetStatus { get; set; } = new();
    public DashboardWidget<List<StatusBreakdownRowDto>> ByCategory { get; set; } = new();
    public DashboardWidget<List<StatusBreakdownRowDto>> ByDepartment { get; set; } = new();
    public DashboardWidget<List<PurchaseYearRowDto>> ByPurchaseYear { get; set; } = new();
    public DashboardWidget<DashboardFailureDto> Failure { get; set; } = new();
    public DashboardWidget<DashboardReplacementDto> Replacement { get; set; } = new();
    public DashboardWidget<DashboardMaintenanceDto> Maintenance { get; set; } = new();
    public DashboardWidget<DashboardLicensesDto> Licenses { get; set; } = new();
    // [ĐÃ ĐÓNG]
    // public DashboardWidget<DashboardBudgetDto> Budget { get; set; } = new();
}

public class DashboardOverviewDto
{
    public int TotalAssets { get; set; }             // không tính đã thanh lý (cùng định nghĩa với module Vòng đời)
    public int DisposedAssets { get; set; }
    public int InUse { get; set; }
    public int Maintenance { get; set; }
    public int Broken { get; set; }
    public int OpenTickets { get; set; }             // phiếu bảo trì đang xử lý (Pending)
}

public class StatusCountDto
{
    public string Status { get; set; } = null!;      // "InUse" | "Maintenance" | "Broken"
    public string Label { get; set; } = null!;
    public int Count { get; set; }
    public double Percent { get; set; }              // 0..1 trên tổng không tính đã thanh lý; 0 khi tổng = 0
}

public class StatusBreakdownRowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Total { get; set; }
    public int InUse { get; set; }
    public int Maintenance { get; set; }
    public int Broken { get; set; }
}

public class PurchaseYearRowDto
{
    public int? Year { get; set; }                   // null = thiếu hoặc sai ngày mua (trống / ở tương lai)
    public string Label { get; set; } = null!;
    public int Count { get; set; }
    public int AssetsWithTicket { get; set; }
    public double FailureRatio { get; set; }         // AssetsWithTicket / Count; 0 khi Count = 0
}

public class DashboardFailureDto
{
    public double FailureRatio { get; set; }         // tài sản có ít nhất 1 phiếu / tổng (như trang Vòng đời)
    public int AssetsWithAtLeastOneTicket { get; set; }
    public IReadOnlyList<LifecycleCategoryStatsDto> ByCategory { get; set; } = Array.Empty<LifecycleCategoryStatsDto>();
    public IReadOnlyList<TicketTrendPointDto> TicketTrend { get; set; } = Array.Empty<TicketTrendPointDto>();
    public IReadOnlyList<TopFailingAssetDto> TopFailingAssets { get; set; } = Array.Empty<TopFailingAssetDto>();
}

public class DashboardReplacementDto
{
    public int OverdueNowCount { get; set; }                    // đã vượt ngưỡng (= "Cần thay thế" của trang Vòng đời)
    public int UpcomingCount { get; set; }                      // sắp tới hạn trong UpcomingMonths tháng
    public int UpcomingMonths { get; set; }
    public IReadOnlyList<LifecycleAgeBucketDto> AgeBuckets { get; set; } = Array.Empty<LifecycleAgeBucketDto>();
    public IReadOnlyList<UpcomingReplacementDto> Upcoming { get; set; } = Array.Empty<UpcomingReplacementDto>();   // tối đa 10 dòng đầu
}

public class DashboardMaintenanceDto
{
    public int Pending { get; set; }
    public int Resolved { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
}

public class DashboardLicensesDto
{
    public int Total { get; set; }
    public int ExpiringSoon { get; set; }            // hết hạn trong ExpiringDays ngày tới (kể cả hôm nay)
    public int Expired { get; set; }
    public int ExpiringDays { get; set; }
}

// [ĐÃ ĐÓNG]
// public class DashboardBudgetDto
// {
//     public int Year { get; set; }
//     public int ForecastCount { get; set; }           // số phòng ban đã có dự báo của năm
//     public int ReplacementCount { get; set; }
//     public int ExpansionCount { get; set; }
//     public decimal ReplacementBudget { get; set; }
//     public decimal ExpansionBudget { get; set; }
//     public decimal TotalBudget { get; set; }         // = thay thế + mua thêm
//     public DateTime? LatestGeneratedAt { get; set; }
// }
