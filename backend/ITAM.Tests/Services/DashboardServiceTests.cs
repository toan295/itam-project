using ITAM.API.Authorization;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class DashboardServiceTests
{
    private static readonly int Year = VietnamTime.Today().Year;

    private readonly Mock<IDashboardRepository> _repo = new();
    private readonly Mock<ILifecycleAnalysisService> _lifecycle = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DashboardService _sut;

    private static readonly IReadOnlySet<string> AdminPerms = new HashSet<string>
    {
        // [ĐÃ ĐÓNG]
        // Permissions.DashboardView, Permissions.LicensesManage, Permissions.ForecastsManage,
        Permissions.DashboardView, Permissions.LicensesManage,
    };
    private static readonly IReadOnlySet<string> ManagerPerms = new HashSet<string> { Permissions.DashboardView };

    public DashboardServiceTests()
    {
        _sut = new DashboardService(_repo.Object, _lifecycle.Object, _cache, NullLogger<DashboardService>.Instance);

        _repo.Setup(r => r.GetDepartmentNameAsync(It.IsAny<int>())).ReturnsAsync((int id) => id is 1 or 2 ? $"Phòng {id}" : null);
        _repo.Setup(r => r.GetAssetStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new Dictionary<AssetStatus, int>
        {
            [AssetStatus.InUse] = 70, [AssetStatus.Maintenance] = 20, [AssetStatus.Broken] = 10, [AssetStatus.Disposed] = 5,
        });
        _repo.Setup(r => r.GetTicketStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new Dictionary<TicketStatus, int>
        {
            [TicketStatus.Pending] = 4, [TicketStatus.Resolved] = 20, [TicketStatus.Failed] = 2,
        });
        _repo.Setup(r => r.GetCategoryStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new List<AssetGroupCount>
        {
            new(1, "Máy tính", AssetStatus.InUse, 40), new(1, "Máy tính", AssetStatus.Broken, 5),
            new(2, "Máy in", AssetStatus.InUse, 30), new(2, "Máy in", AssetStatus.Maintenance, 20), new(2, "Máy in", AssetStatus.Broken, 5),
        });
        _repo.Setup(r => r.GetDepartmentStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new List<AssetGroupCount>
        {
            new(1, "Phòng 1", AssetStatus.InUse, 70), new(2, "Phòng 2", AssetStatus.Maintenance, 20), new(2, "Phòng 2", AssetStatus.Broken, 10),
        });
        _repo.Setup(r => r.GetPurchaseYearCountsAsync(It.IsAny<int?>(), It.IsAny<DateOnly>())).ReturnsAsync(new List<PurchaseYearCount>
        {
            new(0, 10, 1), new(2022, 40, 20), new(2024, 50, 5),
        });
        _repo.Setup(r => r.GetLicenseCountsAsync(It.IsAny<DateOnly>(), It.IsAny<int>())).ReturnsAsync(new LicenseCounts(12, 3, 2));
        // [ĐÃ ĐÓNG]
        // _repo.Setup(r => r.GetBudgetTotalsAsync(It.IsAny<int>(), It.IsAny<int?>()))
            // .ReturnsAsync(new BudgetTotals(6, 10, 4, 200_000_000m, 80_000_000m, new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)));

        _lifecycle.Setup(l => l.GetStatsAsync(It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new LifecycleStatsDto
        {
            TotalAssets = 100, OverdueNowCount = 7, FailureRatio = 0.26, AssetsWithAtLeastOneTicket = 26,
            AgeBuckets = new[] { new LifecycleAgeBucketDto { Label = "< 1 năm", Count = 100 } },
        });
        _lifecycle.Setup(l => l.GetUpcomingAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), 1, 10, null, null, null))
            .ReturnsAsync(new PagedResultDto<UpcomingReplacementDto>
            {
                Items = new List<UpcomingReplacementDto> { new() { AssetCode = "PC-1" } }, Page = 1, PageSize = 10, TotalItems = 13,
            });
    }

    private static DashboardQuery Admin(int? dept = null, int? year = null, bool refresh = false) =>
        new(PermissionCatalog.AdminRole, 1, dept, year, AdminPerms, refresh);

    private static DashboardQuery Manager(int? ownDept, int? requested = null) =>
        new(PermissionCatalog.ManagerRole, ownDept, requested, null, ManagerPerms);

    // ---------- Phạm vi ----------

    [Fact]
    public async Task Admin_WithoutFilter_SeesWholeSystem()
    {
        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Null(result.Scope.DepartmentId);
        Assert.False(result.Scope.IsLocked);
        _repo.Verify(r => r.GetAssetStatusCountsAsync(null), Times.Once);
        _lifecycle.Verify(l => l.GetStatsAsync(null, null), Times.Once);
    }

    [Fact]
    public async Task Admin_WithDepartmentFilter_UsesItEverywhereAndReportsItsName()
    {
        var result = await _sut.GetSummaryAsync(Admin(dept: 2));

        Assert.Equal((2, "Phòng 2", false), (result.Scope.DepartmentId, result.Scope.DepartmentName, result.Scope.IsLocked));
        _repo.Verify(r => r.GetCategoryStatusCountsAsync(2), Times.Once);
        // [ĐÃ ĐÓNG]
        // _repo.Verify(r => r.GetBudgetTotalsAsync(It.IsAny<int>(), 2), Times.Once);
        _lifecycle.Verify(l => l.GetStatsAsync(2, null), Times.Once);
    }

    [Fact]
    public async Task Manager_IsLockedToOwnDepartment_EvenWhenNoDepartmentRequested()
    {
        var result = await _sut.GetSummaryAsync(Manager(1));

        Assert.Equal((1, true), (result.Scope.DepartmentId, result.Scope.IsLocked));
        _repo.Verify(r => r.GetAssetStatusCountsAsync(1), Times.Once);
        _repo.Verify(r => r.GetAssetStatusCountsAsync(null), Times.Never);
    }

    [Fact]
    public async Task Manager_RequestingOwnDepartmentExplicitly_IsAllowed()
    {
        var result = await _sut.GetSummaryAsync(Manager(1, requested: 1));

        Assert.Equal(1, result.Scope.DepartmentId);
    }

    [Fact]
    public async Task Manager_RequestingAnotherDepartment_IsForbiddenAndNothingIsQueried()
    {
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.GetSummaryAsync(Manager(1, requested: 2)));

        _repo.Verify(r => r.GetAssetStatusCountsAsync(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task NonAdminWithoutDepartmentClaim_IsForbiddenRatherThanSeeingEverything()
    {
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.GetSummaryAsync(Manager(null)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(99)]
    public async Task Admin_UnknownOrInvalidDepartment_IsRejected(int dept)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetSummaryAsync(Admin(dept: dept)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Year_OutsideCurrentToPlusTen_IsRejected(int offset)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetSummaryAsync(Admin(year: Year + offset)));
    }

    // ---------- Quyền xem từng widget ----------

    [Fact]
    public async Task Manager_DoesNotGetLicenseOrBudgetWidgets_AndTheirQueriesAreNeverRun()
    {
        var result = await _sut.GetSummaryAsync(Manager(1));

        Assert.Equal("hidden", result.Licenses.Status);
        // [ĐÃ ĐÓNG]
        // Assert.Equal("hidden", result.Budget.Status);
        Assert.Null(result.Licenses.Data);
        _repo.Verify(r => r.GetLicenseCountsAsync(It.IsAny<DateOnly>(), It.IsAny<int>()), Times.Never);
        // [ĐÃ ĐÓNG]
        // _repo.Verify(r => r.GetBudgetTotalsAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
        Assert.Equal("ok", result.Replacement.Status);          // vòng đời trong phòng ban mình vẫn thấy (UC-18)
    }

    [Fact]
    public async Task Admin_GetsLicenseAndBudgetWidgets()
    {
        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Equal((12, 3, 2), (result.Licenses.Data!.Total, result.Licenses.Data.ExpiringSoon, result.Licenses.Data.Expired));
        // [ĐÃ ĐÓNG]
        // var budget = result.Budget.Data!;
        // Assert.Equal(280_000_000m, budget.TotalBudget);                      // thay thế + mua thêm
        // Assert.Equal((6, 14), (budget.ForecastCount, budget.ReplacementCount + budget.ExpansionCount));
    }

    // ---------- Số liệu ----------

    [Fact]
    public async Task Overview_ExcludesDisposedFromTotalAndCountsOpenTickets()
    {
        var overview = (await _sut.GetSummaryAsync(Admin())).Overview.Data!;

        Assert.Equal((100, 5, 70, 20, 10, 4), (overview.TotalAssets, overview.DisposedAssets, overview.InUse, overview.Maintenance, overview.Broken, overview.OpenTickets));
    }

    [Fact]
    public async Task AssetStatus_PercentsSumToOneAndMatchOverviewTotal()
    {
        var result = await _sut.GetSummaryAsync(Admin());

        var statuses = result.AssetStatus.Data!;
        Assert.Equal(new[] { "InUse", "Maintenance", "Broken" }, statuses.Select(s => s.Status));
        Assert.Equal(result.Overview.Data!.TotalAssets, statuses.Sum(s => s.Count));
        Assert.Equal(1d, statuses.Sum(s => s.Percent), 6);
    }

    [Fact]
    public async Task EmptyScope_YieldsZerosWithoutNaN()
    {
        _repo.Setup(r => r.GetAssetStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new Dictionary<AssetStatus, int>());
        _repo.Setup(r => r.GetTicketStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new Dictionary<TicketStatus, int>());
        _repo.Setup(r => r.GetCategoryStatusCountsAsync(It.IsAny<int?>())).ReturnsAsync(new List<AssetGroupCount>());
        _repo.Setup(r => r.GetPurchaseYearCountsAsync(It.IsAny<int?>(), It.IsAny<DateOnly>())).ReturnsAsync(new List<PurchaseYearCount>());

        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Equal(0, result.Overview.Data!.TotalAssets);
        Assert.All(result.AssetStatus.Data!, s => Assert.Equal(0d, s.Percent));
        Assert.Empty(result.ByCategory.Data!);
        Assert.Empty(result.ByPurchaseYear.Data!);
        Assert.Equal(0, result.Maintenance.Data!.Total);
    }

    [Fact]
    public async Task ByCategory_GroupsStatusesIntoOneRowEachSortedByTotalDescending()
    {
        var rows = (await _sut.GetSummaryAsync(Admin())).ByCategory.Data!;

        Assert.Equal(new[] { "Máy in", "Máy tính" }, rows.Select(r => r.Name));          // 55 trước 45
        var printers = rows[0];
        Assert.Equal((55, 30, 20, 5), (printers.Total, printers.InUse, printers.Maintenance, printers.Broken));
        Assert.Equal(100, rows.Sum(r => r.Total));                                         // khớp tổng không tính đã thanh lý
    }

    [Fact]
    public async Task ByPurchaseYear_PutsUnknownLastWithLabelAndComputesRatios()
    {
        var rows = (await _sut.GetSummaryAsync(Admin())).ByPurchaseYear.Data!;

        Assert.Equal(new int?[] { 2022, 2024, null }, rows.Select(r => r.Year));
        Assert.Equal("Không rõ / sai ngày mua", rows[^1].Label);
        Assert.Equal(0.5d, rows[0].FailureRatio, 6);                                       // 20/40
        Assert.Equal(100, rows.Sum(r => r.Count));
    }

    [Fact]
    public async Task FailureAndReplacement_ReuseLifecycleNumbersUnchanged()
    {
        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Equal((0.26, 26), (result.Failure.Data!.FailureRatio, result.Failure.Data.AssetsWithAtLeastOneTicket));
        Assert.Equal(7, result.Replacement.Data!.OverdueNowCount);
        Assert.Equal((13, 12), (result.Replacement.Data.UpcomingCount, result.Replacement.Data.UpcomingMonths));
        Assert.Equal("PC-1", Assert.Single(result.Replacement.Data.Upcoming).AssetCode);
        _lifecycle.Verify(l => l.GetStatsAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);   // tính một lần cho cả hai widget
    }

    // ---------- Một widget lỗi không làm hỏng cả trang ----------

    [Fact]
    public async Task OneFailingWidget_ReportsErrorForItselfOnly()
    {
        _repo.Setup(r => r.GetCategoryStatusCountsAsync(It.IsAny<int?>())).ThrowsAsync(new InvalidOperationException("boom: secret detail"));

        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Equal("error", result.ByCategory.Status);
        Assert.DoesNotContain("secret", result.ByCategory.Message);
        Assert.Equal("ok", result.Overview.Status);
        Assert.Equal("ok", result.ByDepartment.Status);
        // [ĐÃ ĐÓNG]
        // Assert.Equal("ok", result.Budget.Status);
    }

    [Fact]
    public async Task LifecycleFailure_BreaksOnlyTheWidgetsThatDependOnIt()
    {
        _lifecycle.Setup(l => l.GetStatsAsync(It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(new InvalidOperationException("db down"));

        var result = await _sut.GetSummaryAsync(Admin());

        Assert.Equal("error", result.Failure.Status);
        Assert.Equal("error", result.Replacement.Status);
        Assert.Equal("ok", result.Overview.Status);
        Assert.Equal("ok", result.Maintenance.Status);
        Assert.Equal("ok", result.Licenses.Status);
    }

    // ---------- Cache ----------

    [Fact]
    public async Task SecondCall_WithinCacheWindow_IsServedFromCacheWithoutQueries()
    {
        var first = await _sut.GetSummaryAsync(Admin());
        var second = await _sut.GetSummaryAsync(Admin());

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(first.GeneratedAt, second.GeneratedAt);                 // hiển thị đúng lúc tính, không giả là mới
        _repo.Verify(r => r.GetAssetStatusCountsAsync(null), Times.Once);
        _lifecycle.Verify(l => l.GetStatsAsync(null, null), Times.Once);
    }

    [Fact]
    public async Task Refresh_BypassesCacheAndReplacesIt()
    {
        await _sut.GetSummaryAsync(Admin());
        var refreshed = await _sut.GetSummaryAsync(Admin(refresh: true));
        var after = await _sut.GetSummaryAsync(Admin());

        Assert.False(refreshed.FromCache);
        Assert.True(after.FromCache);
        _repo.Verify(r => r.GetAssetStatusCountsAsync(null), Times.Exactly(2));
    }

    [Fact]
    public async Task Cache_IsSeparatedByScopeAndByWhatTheViewerMaySee()
    {
        await _sut.GetSummaryAsync(Admin());
        var otherDept = await _sut.GetSummaryAsync(Admin(dept: 2));
        var manager = await _sut.GetSummaryAsync(Manager(1));
        var adminAgain = await _sut.GetSummaryAsync(Admin());

        Assert.False(otherDept.FromCache);
        Assert.False(manager.FromCache);                                     // không dùng chung bản của Admin dù cùng phòng ban/không
        Assert.Equal("hidden", manager.Licenses.Status);                      // bản cache của Admin (có giấy phép) không lọt sang Manager
        Assert.True(adminAgain.FromCache);
        Assert.Equal("ok", adminAgain.Licenses.Status);
    }

    [Fact]
    public async Task Cache_ManagerAndAdminFilteredToSameDepartment_DoNotShareEntries()
    {
        var admin = await _sut.GetSummaryAsync(Admin(dept: 1));
        var manager = await _sut.GetSummaryAsync(Manager(1));

        Assert.False(manager.FromCache);
        // [ĐÃ ĐÓNG]
        // Assert.Equal("ok", admin.Budget.Status);
        Assert.Equal("ok", admin.Licenses.Status);
        // [ĐÃ ĐÓNG]
        // Assert.Equal("hidden", manager.Budget.Status);
        Assert.Equal("hidden", manager.Licenses.Status);
    }

    [Fact]
    public async Task ConcurrentFirstCalls_ComputeOnlyOnce()
    {
        var calls = Enumerable.Range(0, 8).Select(_ => _sut.GetSummaryAsync(Admin(dept: 1))).ToArray();
        var results = await Task.WhenAll(calls);

        _repo.Verify(r => r.GetAssetStatusCountsAsync(1), Times.Once);
        Assert.Equal(1, results.Count(r => !r.FromCache));
    }
}
