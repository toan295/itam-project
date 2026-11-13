using System.Collections.Concurrent;
using ITAM.API.Authorization;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Dashboard;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ITAM.API.Services.Implementations;

// UC-18. Mọi số liệu tuổi/lỗi/tới hạn lấy từ ILifecycleAnalysisService (cùng nguồn với trang Vòng đời) nên Dashboard, Vòng đời và
// báo cáo xuất file luôn khớp nhau. Số đếm theo trạng thái/loại/phòng ban/năm mua tính ở DB (GROUP BY).
public class DashboardService : IDashboardService
{
    public const int CacheSeconds = 60;
    public const int UpcomingMonths = 12;
    public const int UpcomingListSize = 10;
    public const int LicenseExpiringDays = 30;
    private const int MaxYearsAhead = 10;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private static readonly (AssetStatus Status, string Label)[] ShownStatuses =
    {
        (AssetStatus.InUse, "Đang sử dụng"),
        (AssetStatus.Maintenance, "Đang bảo trì"),
        (AssetStatus.Broken, "Hỏng"),
    };

    private readonly IDashboardRepository _repo;
    private readonly ILifecycleAnalysisService _lifecycle;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IDashboardRepository repo,
        ILifecycleAnalysisService lifecycle,
        IMemoryCache cache,
        ILogger<DashboardService> logger)
    {
        _repo = repo;
        _lifecycle = lifecycle;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query)
    {
        var today = VietnamTime.Today();
        var year = query.Year ?? today.Year;
        if (year < today.Year || year > today.Year + MaxYearsAhead)
        {
            throw new ArgumentException($"Năm phải từ {today.Year} đến {today.Year + MaxYearsAhead}.");
        }

        var (departmentId, locked) = ViewerScope.Resolve(query.Role, query.UserDepartmentId, query.RequestedDepartmentId);
        string? departmentName = null;
        if (departmentId.HasValue)
        {
            // Kiểm tra trước khi tính/tạo khoá cache: id không tồn tại (hoặc <= 0) bị từ chối, không làm phình bộ nhớ.
            departmentName = departmentId.Value > 0 ? await _repo.GetDepartmentNameAsync(departmentId.Value) : null;
            if (departmentName is null)
            {
                throw new ArgumentException("Phòng ban không tồn tại.");
            }
        }

        var canLicenses = query.Permissions.Contains(Permissions.LicensesManage);
        // [ĐÃ ĐÓNG]
        // var canBudget = query.Permissions.Contains(Permissions.ForecastsManage);

        // Cùng phạm vi + cùng quyền xem + cùng năm => cùng dữ liệu, nên dùng chung bản cache (khoá không chứa danh tính người dùng).
        var key = $"dashboard:{departmentId?.ToString() ?? "all"}:{(locked ? "L" : "A")}:{year}:{(canLicenses ? 1 : 0)}";   // [ĐÃ ĐÓNG] trước đây thêm {(canBudget ? 1 : 0)}
        if (!query.Refresh && _cache.TryGetValue(key, out DashboardSummaryDto? hit) && hit is not null)
        {
            return Clone(hit, fromCache: true);
        }

        // Một yêu cầu tính cho mỗi khoá tại một thời điểm: nhiều người mở Dashboard cùng lúc không làm lặp truy vấn tổng hợp nặng.
        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (!query.Refresh && _cache.TryGetValue(key, out hit) && hit is not null)
            {
                return Clone(hit, fromCache: true);
            }

            var summary = await BuildAsync(departmentId, departmentName, locked, year, today, canLicenses /* [ĐÃ ĐÓNG] , canBudget */);
            _cache.Set(key, summary, TimeSpan.FromSeconds(CacheSeconds));
            return Clone(summary, fromCache: false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<DashboardSummaryDto> BuildAsync(int? departmentId, string? departmentName, bool locked, int year, DateOnly today, bool canLicenses /* [ĐÃ ĐÓNG] , bool canBudget */)
    {
        var summary = new DashboardSummaryDto
        {
            GeneratedAt = DateTime.UtcNow,
            CacheSeconds = CacheSeconds,
            Year = year,
            Scope = new DashboardScopeDto { DepartmentId = departmentId, DepartmentName = departmentName, IsLocked = locked },
        };

        // Các widget dùng chung một DbContext nên chạy lần lượt (EF không cho truy vấn song song trên một context).
        // Thống kê Vòng đời tính một lần, dùng cho nhiều widget; nếu lỗi thì chỉ các widget phụ thuộc báo lỗi.
        Task<LifecycleStatsDto>? lifecycleStats = null;
        Task<LifecycleStatsDto> Stats() => lifecycleStats ??= _lifecycle.GetStatsAsync(departmentId);

        var statusCounts = new Lazy<Task<Dictionary<AssetStatus, int>>>(() => _repo.GetAssetStatusCountsAsync(departmentId));
        var ticketCounts = new Lazy<Task<Dictionary<TicketStatus, int>>>(() => _repo.GetTicketStatusCountsAsync(departmentId));

        summary.Overview = await Safe("Overview", async () =>
        {
            var counts = await statusCounts.Value;
            var tickets = await ticketCounts.Value;
            var inUse = counts.GetValueOrDefault(AssetStatus.InUse);
            var maintenance = counts.GetValueOrDefault(AssetStatus.Maintenance);
            var broken = counts.GetValueOrDefault(AssetStatus.Broken);
            return new DashboardOverviewDto
            {
                TotalAssets = inUse + maintenance + broken,
                DisposedAssets = counts.GetValueOrDefault(AssetStatus.Disposed),
                InUse = inUse,
                Maintenance = maintenance,
                Broken = broken,
                OpenTickets = tickets.GetValueOrDefault(TicketStatus.Pending),
            };
        });

        summary.AssetStatus = await Safe("AssetStatus", async () =>
        {
            var counts = await statusCounts.Value;
            var total = ShownStatuses.Sum(s => counts.GetValueOrDefault(s.Status));
            return ShownStatuses.Select(s => new StatusCountDto
            {
                Status = s.Status.ToString(),
                Label = s.Label,
                Count = counts.GetValueOrDefault(s.Status),
                Percent = total == 0 ? 0 : (double)counts.GetValueOrDefault(s.Status) / total,
            }).ToList();
        });

        summary.ByCategory = await Safe("ByCategory", async () => ToRows(await _repo.GetCategoryStatusCountsAsync(departmentId)));
        summary.ByDepartment = await Safe("ByDepartment", async () => ToRows(await _repo.GetDepartmentStatusCountsAsync(departmentId)));

        summary.ByPurchaseYear = await Safe("ByPurchaseYear", async () =>
            (await _repo.GetPurchaseYearCountsAsync(departmentId, today)).Select(r => new PurchaseYearRowDto
            {
                Year = r.Year == 0 ? null : r.Year,
                Label = r.Year == 0 ? "Không rõ / sai ngày mua" : r.Year.ToString(),
                Count = r.Count,
                AssetsWithTicket = r.WithTicket,
                FailureRatio = r.Count == 0 ? 0 : (double)r.WithTicket / r.Count,
            }).OrderBy(r => r.Year is null ? 1 : 0).ThenBy(r => r.Year).ToList());

        summary.Failure = await Safe("Failure", async () =>
        {
            var stats = await Stats();
            return new DashboardFailureDto
            {
                FailureRatio = stats.FailureRatio,
                AssetsWithAtLeastOneTicket = stats.AssetsWithAtLeastOneTicket,
                ByCategory = stats.ByCategory,
                TicketTrend = stats.TicketTrend,
                TopFailingAssets = stats.TopFailingAssets,
            };
        });

        summary.Replacement = await Safe("Replacement", async () =>
        {
            var stats = await Stats();
            var upcoming = await _lifecycle.GetUpcomingAsync(departmentId, null, UpcomingMonths, 1, UpcomingListSize);
            return new DashboardReplacementDto
            {
                OverdueNowCount = stats.OverdueNowCount,
                UpcomingCount = upcoming.TotalItems,
                UpcomingMonths = UpcomingMonths,
                AgeBuckets = stats.AgeBuckets,
                Upcoming = upcoming.Items,
            };
        });

        summary.Maintenance = await Safe("Maintenance", async () =>
        {
            var tickets = await ticketCounts.Value;
            var pending = tickets.GetValueOrDefault(TicketStatus.Pending);
            var resolved = tickets.GetValueOrDefault(TicketStatus.Resolved);
            var failed = tickets.GetValueOrDefault(TicketStatus.Failed);
            return new DashboardMaintenanceDto { Pending = pending, Resolved = resolved, Failed = failed, Total = pending + resolved + failed };
        });

        summary.Licenses = canLicenses
            ? await Safe("Licenses", async () =>
            {
                var counts = await _repo.GetLicenseCountsAsync(today, LicenseExpiringDays);
                return new DashboardLicensesDto { Total = counts.Total, ExpiringSoon = counts.ExpiringSoon, Expired = counts.Expired, ExpiringDays = LicenseExpiringDays };
            })
            : Hidden<DashboardLicensesDto>();

        // [ĐÃ ĐÓNG] Widget ngân sách của Dashboard (đọc bảng BudgetForecasts)
        // summary.Budget = canBudget
        // ? await Safe("Budget", async () =>
        // {
        // var totals = await _repo.GetBudgetTotalsAsync(year, departmentId);
        // return new DashboardBudgetDto
        // {
        // Year = year,
        // ForecastCount = totals.ForecastCount,
        // ReplacementCount = totals.ReplacementCount,
        // ExpansionCount = totals.ExpansionCount,
        // ReplacementBudget = totals.ReplacementBudget,
        // ExpansionBudget = totals.ExpansionBudget,
        // TotalBudget = totals.ReplacementBudget + totals.ExpansionBudget,
        // LatestGeneratedAt = totals.LatestGeneratedAt,
        // };
        // })
        // : Hidden<DashboardBudgetDto>();

        return summary;
    }

    private async Task<DashboardWidget<T>> Safe<T>(string name, Func<Task<T>> build)
    {
        try
        {
            return new DashboardWidget<T> { Status = "ok", Data = await build() };
        }
        catch (Exception ex)
        {
            // Không lộ nội dung ngoại lệ ra ngoài; widget khác vẫn hiển thị bình thường.
            _logger.LogError(ex, "Widget Dashboard '{Widget}' không tính được.", name);
            return new DashboardWidget<T> { Status = "error", Message = "Không tải được số liệu này. Vui lòng thử lại sau." };
        }
    }

    private static DashboardWidget<T> Hidden<T>() => new() { Status = "hidden" };

    // Gom (id, tên, trạng thái, số lượng) thành một dòng cho mỗi id; trạng thái Disposed không được truyền vào đây.
    private static List<StatusBreakdownRowDto> ToRows(IEnumerable<AssetGroupCount> groups) =>
        groups.GroupBy(g => (g.Id, g.Name))
            .Select(g => new StatusBreakdownRowDto
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                InUse = g.Where(x => x.Status == AssetStatus.InUse).Sum(x => x.Count),
                Maintenance = g.Where(x => x.Status == AssetStatus.Maintenance).Sum(x => x.Count),
                Broken = g.Where(x => x.Status == AssetStatus.Broken).Sum(x => x.Count),
                Total = g.Sum(x => x.Count),
            })
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

    // Trả bản sao nông: bản trong cache không bị sửa (FromCache) khi nhiều người dùng chung; các widget bên trong không đổi sau khi dựng.
    private static DashboardSummaryDto Clone(DashboardSummaryDto s, bool fromCache) => new()
    {
        GeneratedAt = s.GeneratedAt,
        FromCache = fromCache,
        CacheSeconds = s.CacheSeconds,
        Year = s.Year,
        Scope = s.Scope,
        Overview = s.Overview,
        AssetStatus = s.AssetStatus,
        ByCategory = s.ByCategory,
        ByDepartment = s.ByDepartment,
        ByPurchaseYear = s.ByPurchaseYear,
        Failure = s.Failure,
        Replacement = s.Replacement,
        Maintenance = s.Maintenance,
        Licenses = s.Licenses,
        // [ĐÃ ĐÓNG]
        // Budget = s.Budget,
    };
}
