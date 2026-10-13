using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Microsoft.Extensions.Options;
using Moq;

namespace ITAM.Tests.Services;

public class LifecycleAnalysisServiceTests
{
    private const int CatComputer = 10;
    private const int CatSwitch = 12;

    // Dựng dữ liệu theo ngày tương đối so với hôm nay để test không phụ thuộc ngày chạy.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly int Year = Today.Year;

    private readonly Mock<ILifecycleRepository> _repo = new();
    private readonly LifecycleAnalysisService _sut;

    public LifecycleAnalysisServiceTests()
    {
        _sut = new LifecycleAnalysisService(
            _repo.Object,
            Options.Create(new LifecycleOptions { DefaultMaxAgeYears = 5, DefaultMaxFailureCount = 3 }));

        _repo.Setup(r => r.GetPoliciesAsync()).ReturnsAsync(new List<AssetCategoryLifecyclePolicy>());
        _repo.Setup(r => r.GetTicketCountByYearAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<TicketTrendPointDto>());
        _repo.Setup(r => r.GetCategoriesAsync()).ReturnsAsync(new List<AssetCategory>
        {
            new() { Id = CatComputer, Name = "Máy tính" },
            new() { Id = CatSwitch, Name = "Switch" },
        });
        SetupRows();
    }

    private void SetupRows(params AssetLifecycleRow[] rows) =>
        _repo.Setup(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>())).ReturnsAsync(rows.ToList());

    private void SetupPolicies(params AssetCategoryLifecyclePolicy[] policies) =>
        _repo.Setup(r => r.GetPoliciesAsync()).ReturnsAsync(policies.ToList());

    private static AssetLifecycleRow Row(
        string code, int categoryId, DateOnly? purchase, int tickets = 0, int failed = 0, int departmentId = 1,
        DateOnly? warranty = null, string? departmentName = null) => new()
    {
        AssetId = Math.Abs(code.GetHashCode()),
        AssetCode = code,
        AssetName = code,
        CategoryId = categoryId,
        CategoryName = categoryId == CatComputer ? "Máy tính" : "Switch",
        DepartmentId = departmentId,
        DepartmentName = departmentName ?? (departmentId == 1 ? "IT-A" : $"IT-{(char)('A' + departmentId - 1)}"),
        PurchaseDate = purchase,
        WarrantyExpiry = warranty,
        TicketCount = tickets,
        FailedTicketCount = failed,
    };

    // ---------- GetReplacementCandidatesAsync: hợp đồng với module Dự báo ----------

    [Fact]
    public async Task GetReplacementCandidatesAsync_NoPolicyForCategory_UsesDefaultFromOptions()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-5)));

        var candidate = Assert.Single(await _sut.GetReplacementCandidatesAsync(null, Year));

        Assert.True(candidate.IsOverdueNow);
        Assert.Equal((5, 3), (candidate.MaxAgeYears, candidate.MaxFailureCount));
        Assert.Equal(new[] { LifecycleCalculator.ReasonAgeExceeded }, candidate.Reasons);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_CategoryPolicyOverridesDefault()
    {
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 3 });
        SetupRows(
            Row("S1", CatSwitch, Today.AddYears(-6)),       // 6 < 7 → chưa quá tuổi
            Row("P1", CatComputer, Today.AddYears(-6)));    // mặc định 5 → quá tuổi

        var result = await _sut.GetReplacementCandidatesAsync(null, Year + 5);

        var sw = result.Single(c => c.AssetCode == "S1");
        Assert.False(sw.IsOverdueNow);
        Assert.Equal(Today.AddYears(-6).AddYears(7).Year, sw.DueYear);
        Assert.True(result.Single(c => c.AssetCode == "P1").IsOverdueNow);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_HorizonYearBelowDueYear_ExcludesAsset()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-2)));   // DueYear = Year + 3

        Assert.Empty(await _sut.GetReplacementCandidatesAsync(null, Year + 2));
        Assert.Single(await _sut.GetReplacementCandidatesAsync(null, Year + 3));
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_HorizonYearBeforeCurrentYear_ReturnsEmpty()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-9)));

        Assert.Empty(await _sut.GetReplacementCandidatesAsync(null, Year - 1));
        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_PassesDepartmentFilterToRepository()
    {
        await _sut.GetReplacementCandidatesAsync(7, Year);

        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(7), Times.Once);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_AssetWithoutDueYear_IsExcluded()
    {
        SetupRows(Row("A1", CatComputer, null, tickets: 1));   // không ngày mua, chưa vượt ngưỡng lỗi

        Assert.Empty(await _sut.GetReplacementCandidatesAsync(null, Year + 10));
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_LongOverdueAsset_HasDueYearEqualToCurrentYear()
    {
        // Hồi quy: DueYear không được nằm ở quá khứ, nếu không dự báo năm hiện tại sẽ bỏ sót tài sản.
        SetupRows(Row("OLD", CatComputer, Today.AddYears(-9)));

        var candidate = Assert.Single(await _sut.GetReplacementCandidatesAsync(null, Year));

        Assert.Equal(Year, candidate.DueYear);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_FailureThresholdUsesAllTicketsNotOnlyFailed()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-2), tickets: 3, failed: 0));

        var candidate = Assert.Single(await _sut.GetReplacementCandidatesAsync(null, Year));

        Assert.Equal(new[] { LifecycleCalculator.ReasonFailureCountExceeded }, candidate.Reasons);
        Assert.Equal(Year, candidate.DueYear);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_ResultSortedByOverdueThenPriorityThenAgeThenCode()
    {
        SetupRows(
            Row("C-NOTYET", CatComputer, Today.AddYears(-2)),               // chưa quá hạn → cuối
            Row("B-OLD", CatComputer, Today.AddYears(-8)),                  // quá tuổi 8 năm
            Row("A-YOUNG", CatComputer, Today.AddYears(-2), tickets: 3),    // quá số lần lỗi, tuổi 2
            Row("D-BOTH", CatComputer, Today.AddYears(-6), tickets: 3),     // cả 2 → ưu tiên cao nhất
            Row("A-OLD", CatComputer, Today.AddYears(-8)));                 // cùng tuổi B-OLD → theo mã

        var codes = (await _sut.GetReplacementCandidatesAsync(null, Year + 5)).Select(c => c.AssetCode).ToList();

        Assert.Equal(new[] { "D-BOTH", "A-OLD", "B-OLD", "A-YOUNG", "C-NOTYET" }, codes);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_EachAssetAppearsExactlyOnce()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-9), tickets: 5),
            Row("A2", CatComputer, Today.AddYears(-1)));

        var result = await _sut.GetReplacementCandidatesAsync(null, Year + 10);

        Assert.Equal(result.Count, result.Select(c => c.AssetId).Distinct().Count());
        Assert.All(result, c => Assert.True(c.DueYear >= Year));
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_ScenarioC2_MatchesExpectedForecastInputs()
    {
        // Rút gọn kịch bản C.2 (IT-A): S1 6 năm, S2 2 năm/3 phiếu, S3 2 năm/2 phiếu, S4 đúng 5 năm,
        // S5 (Máy in) 8 năm, S6 Switch 6 năm (ngưỡng riêng 7).
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 3 });
        SetupRows(
            Row("S1", CatComputer, Today.AddYears(-6)),
            Row("S2", CatComputer, Today.AddYears(-2), tickets: 3),
            Row("S3", CatComputer, Today.AddYears(-2), tickets: 2),
            Row("S4", CatComputer, Today.AddYears(-5)),
            Row("S5", CatComputer, Today.AddYears(-8)),
            Row("S6", CatSwitch, Today.AddYears(-6)));

        var thisYear = await _sut.GetReplacementCandidatesAsync(null, Year);
        var nextYearOnly = (await _sut.GetReplacementCandidatesAsync(null, Year + 1))
            .Where(c => c.DueYear == Year + 1).ToList();

        Assert.Equal(new[] { "S5", "S1", "S4", "S2" }, thisYear.Select(c => c.AssetCode));
        Assert.Equal("S6", Assert.Single(nextYearOnly).AssetCode);
    }

    // ---------- GetCurrentReplacementCandidatesAsync ----------

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_OnlyReturnsOverdueNowAssets()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-6)),
            Row("A2", CatComputer, Today.AddYears(-2)));   // sắp tới hạn nhưng chưa vượt

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20);

        Assert.Equal("A1", Assert.Single(result.Items).AssetCode);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_CategoryFilterApplied()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-6)),
            Row("S1", CatSwitch, Today.AddYears(-6)));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, CatSwitch, "score", 1, 20);

        Assert.Equal("S1", Assert.Single(result.Items).AssetCode);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_PaginatesAndNormalizesPaging()
    {
        SetupRows(Enumerable.Range(1, 130).Select(i => Row($"A{i:D3}", CatComputer, Today.AddYears(-6))).ToArray());

        var page1 = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 100);
        var page2 = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 2, 100);
        var outOfRange = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", int.MaxValue, 5000);   // không được tràn số / lỗi

        Assert.Equal(100, page1.Items.Count);
        Assert.Equal(30, page2.Items.Count);
        Assert.Equal(130, page1.TotalItems);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(20, outOfRange.PageSize);   // pageSize ngoài [1, 100] quay về mặc định như các module khác.
        Assert.Empty(outOfRange.Items);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_NothingOverdue_ReturnsEmptyList()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-1)));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_InvalidSortBy_ThrowsArgumentException()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-6)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetCurrentReplacementCandidatesAsync(null, null, "unknown", 1, 20));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_SortByFailures_UsesTicketCountThenAssetCode()
    {
        SetupRows(
            Row("B", CatComputer, Today.AddYears(-6), tickets: 4),
            Row("A", CatComputer, Today.AddYears(-6), tickets: 4),
            Row("C", CatComputer, Today.AddYears(-6), tickets: 3));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "failures", 1, 20);

        Assert.Equal(new[] { "A", "B", "C" }, result.Items.Select(item => item.AssetCode));
    }

    // ---------- GetUpcomingAsync ----------

    [Fact]
    public async Task GetUpcomingAsync_ReturnsOnlyNotOverdueUpcomingAssets()
    {
        SetupRows(
            Row("OVERDUE", CatComputer, Today.AddYears(-6)),
            Row("AGE", CatComputer, Today.AddYears(-5).AddMonths(3)),
            Row("FAIL", CatComputer, null, tickets: 2),
            Row("NONE", CatComputer, Today.AddYears(-1)));

        var result = await _sut.GetUpcomingAsync(null, null, 12, 1, 20);

        Assert.Equal(2, result.TotalItems);
        Assert.Contains(result.Items, item => item.AssetCode == "AGE" && item.Reasons.Contains(LifecycleCalculator.ReasonAgeDueSoon));
        Assert.Contains(result.Items, item => item.AssetCode == "FAIL" && item.Reasons.Contains(LifecycleCalculator.ReasonNearFailureThreshold));
        Assert.DoesNotContain(result.Items, item => item.AssetCode == "OVERDUE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task GetUpcomingAsync_MonthsOutsideRange_ThrowsArgumentException(int months)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetUpcomingAsync(null, null, months, 1, 20));
    }

    // ---------- GetStatsAsync ----------

    [Fact]
    public async Task GetStatsAsync_NoAssets_ReturnsZerosWithoutDividingByZero()
    {
        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(0, stats.TotalAssets);
        Assert.Equal(0d, stats.FailureRatio);
        Assert.Equal(0d, stats.AverageTicketsPerAsset);
        Assert.Null(stats.AverageAgeYears);
        Assert.Empty(stats.ByCategory);
    }

    [Fact]
    public async Task GetStatsAsync_AverageAgeIgnoresAssetsWithoutPurchaseDate()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-4)),
            Row("A2", CatComputer, Today.AddYears(-2)),
            Row("A3", CatComputer, null));

        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(3, stats.TotalAssets);
        Assert.Equal(1, stats.AssetsWithoutPurchaseDate);
        Assert.InRange(stats.AverageAgeYears!.Value, 2.98, 3.02);
    }

    [Fact]
    public async Task GetStatsAsync_FailureRatio_IsAssetsWithTicketsDividedByTotal()
    {
        SetupRows(
            Row("A1", CatComputer, null, tickets: 3),
            Row("A2", CatComputer, null, tickets: 1),
            Row("A3", CatComputer, null),
            Row("A4", CatComputer, null));

        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(2, stats.AssetsWithAtLeastOneTicket);
        Assert.Equal(0.5, stats.FailureRatio);
        Assert.Equal(1.0, stats.AverageTicketsPerAsset);
    }

    [Fact]
    public async Task GetStatsAsync_ScenarioC2_MatchesExpectedNumbers()
    {
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 3 });
        SetupRows(
            Row("S1", CatComputer, Today.AddYears(-6)),
            Row("S2", CatComputer, Today.AddYears(-2), tickets: 3),
            Row("S3", CatComputer, Today.AddYears(-2), tickets: 2),
            Row("S4", CatComputer, Today.AddYears(-5)),
            Row("S5", CatComputer, Today.AddYears(-8)),
            Row("S6", CatSwitch, Today.AddYears(-6)),
            Row("S8", CatComputer, null, tickets: 1));

        var stats = await _sut.GetStatsAsync(1);

        Assert.Equal(7, stats.TotalAssets);
        Assert.Equal(1, stats.AssetsWithoutPurchaseDate);
        Assert.Equal(3, stats.AssetsWithAtLeastOneTicket);
        Assert.Equal(3d / 7d, stats.FailureRatio, 4);
        Assert.InRange(stats.AverageAgeYears!.Value, 4.78, 4.88);   // (6+2+2+5+8+6)/6 = 4,83
        Assert.Equal(4, stats.OverdueNowCount);                      // S1, S2, S4, S5
    }

    [Fact]
    public async Task GetStatsAsync_ByCategory_ShowsEffectiveThresholdsAndCounts()
    {
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 4 });
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-6), tickets: 1),
            Row("S1", CatSwitch, null));

        var stats = await _sut.GetStatsAsync(null);

        var sw = stats.ByCategory.Single(c => c.CategoryId == CatSwitch);
        Assert.Equal((7, 4), (sw.MaxAgeYears, sw.MaxFailureCount));
        var pc = stats.ByCategory.Single(c => c.CategoryId == CatComputer);
        Assert.Equal((5, 3), (pc.MaxAgeYears, pc.MaxFailureCount));
        Assert.Equal(1, pc.AssetCount);
        Assert.Equal(1, pc.AssetsWithAtLeastOneTicket);
        Assert.Equal(1, pc.OverdueNowCount);
    }

    [Fact]
    public async Task GetStatsAsync_AgeBucketsSumToTotalAssets()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddMonths(-3)),
            Row("A2", CatComputer, Today.AddYears(-2)),
            Row("A3", CatComputer, Today.AddYears(-4)),
            Row("A4", CatComputer, Today.AddYears(-7)),
            Row("A5", CatComputer, null));

        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(new[] { 1, 1, 1, 1, 1 }, stats.AgeBuckets.Select(b => b.Count));
        Assert.Equal(stats.TotalAssets, stats.AgeBuckets.Sum(b => b.Count));
    }

    [Fact]
    public async Task GetStatsAsync_MedianAgeAndExtendedAgeBuckets_AreCalculated()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-1), tickets: 1),
            Row("A2", CatComputer, Today.AddYears(-3), tickets: 0),
            Row("A3", CatComputer, Today.AddYears(-5), tickets: 2));

        var stats = await _sut.GetStatsAsync(null);

        Assert.InRange(stats.MedianAgeYears!.Value, 2.98, 3.02);
        Assert.Equal(stats.TotalAssets, stats.AgeBuckets.Sum(bucket => bucket.Count));
        Assert.Equal(2, stats.AgeBuckets.Sum(bucket => bucket.AssetsWithAtLeastOneTicket));
    }

    [Fact]
    public async Task GetStatsAsync_TicketTrend_FillsFiveYearsIncludingZeros()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-1)));
        _repo.Setup(r => r.GetTicketCountByYearAsync(null, Year - 4))
            .ReturnsAsync(new List<TicketTrendPointDto>
            {
                new() { Year = Year - 2, TicketCount = 4 },
                new() { Year = Year, TicketCount = 2 },
            });

        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(5, stats.TicketTrend.Count);
        Assert.Equal(4, stats.TicketTrend.Single(point => point.Year == Year - 2).TicketCount);
        Assert.Equal(0, stats.TicketTrend.Single(point => point.Year == Year - 1).TicketCount);
        Assert.Equal(2, stats.TicketTrend.Single(point => point.Year == Year).TicketCount);
    }

    // ---------- Tuần 8: hợp đồng C.1 không đổi + một đường quyết định ứng viên ----------

    [Fact]
    public async Task GetReplacementCandidatesAsync_StillReturnsContractOrderAfterEnhancements()
    {
        // Bẫy: X điểm ưu tiên 100 (6 phiếu / ngưỡng 3) nhưng tuổi thấp; Y điểm 90 (9 năm / ngưỡng 5) nhưng tuổi cao.
        // Hợp đồng sắp theo IsOverdueNow ↓, IsHighPriority ↓, AgeYears ↓, AssetCode ↑ — KHÔNG theo PriorityScore.
        SetupRows(
            Row("X-SCORE100", CatComputer, Today.AddYears(-2), tickets: 6),
            Row("Y-AGE9", CatComputer, Today.AddYears(-9)),
            Row("Z-BOTH", CatComputer, Today.AddYears(-6), tickets: 3));

        var codes = (await _sut.GetReplacementCandidatesAsync(null, Year)).Select(c => c.AssetCode).ToList();

        Assert.Equal(new[] { "Z-BOTH", "Y-AGE9", "X-SCORE100" }, codes);
    }

    [Fact]
    public async Task GetReplacementCandidatesAsync_ReturnsContractTypeWithBaseFieldsIntact()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-6), tickets: 1, failed: 1));

        var candidate = Assert.Single(await _sut.GetReplacementCandidatesAsync(null, Year));

        Assert.Equal(("A1", 1, 1, 5, 3, Year), (candidate.AssetCode, candidate.TicketCount, candidate.FailedTicketCount,
            candidate.MaxAgeYears, candidate.MaxFailureCount, candidate.DueYear));
        Assert.Equal(1d / Math.Max(candidate.AgeYears!.Value, 1d), candidate.FailureRatePerYear, 6);
    }

    [Fact]
    public async Task GetCurrentAndContractCandidates_AgreeOnWhoIsAnOverdueCandidate()
    {
        // Quy tắc 12: hàm hợp đồng và hàm giao diện cùng một đường quyết định.
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 3 });
        SetupRows(
            Row("S1", CatComputer, Today.AddYears(-6)),
            Row("S2", CatComputer, Today.AddYears(-2), tickets: 3),
            Row("S3", CatComputer, Today.AddYears(-2), tickets: 2),
            Row("S4", CatComputer, Today.AddYears(-5)),
            Row("S5", CatSwitch, Today.AddYears(-6)),
            Row("S6", CatSwitch, Today.AddYears(-8)));

        var contract = (await _sut.GetReplacementCandidatesAsync(null, Year)).Select(c => c.AssetCode).OrderBy(c => c).ToList();
        var current = (await _sut.GetCurrentReplacementCandidatesAsync(null, null, "code", 1, 100)).Items.Select(c => c.AssetCode).ToList();

        Assert.Equal(contract, current);
        Assert.Equal(new[] { "S1", "S2", "S4", "S6" }, current);
    }

    // ---------- Tuần 8: đề xuất có xếp hạng ----------

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_DefaultSort_OrdersByPriorityScoreDescending()
    {
        SetupRows(
            Row("S12", CatComputer, Today.AddYears(-6), tickets: 1),    // điểm 60
            Row("S13", CatComputer, Today.AddYears(-2), tickets: 6),    // điểm 100
            Row("OLD", CatComputer, Today.AddYears(-8)));               // điểm 80

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20);

        Assert.Equal(new[] { "S13", "OLD", "S12" }, result.Items.Select(i => i.AssetCode));
        Assert.Equal(new[] { 100, 80, 60 }, result.Items.Select(i => i.PriorityScore));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_ScoreTie_BrokenByAgeThenCode()
    {
        SetupRows(
            Row("B", CatComputer, null, tickets: 6),                    // điểm 100, tuổi không rõ → cuối trong nhóm hoà
            Row("C", CatComputer, Today.AddYears(-10)),                 // điểm 100, tuổi 10
            Row("A", CatComputer, Today.AddYears(-10)));                // điểm 100, tuổi 10 → theo mã

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20);

        Assert.Equal(new[] { "A", "C", "B" }, result.Items.Select(i => i.AssetCode));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_NullOrBlankSortBy_UsesScore()
    {
        SetupRows(
            Row("LOW", CatComputer, Today.AddYears(-6)),
            Row("HIGH", CatComputer, Today.AddYears(-2), tickets: 6));

        var blank = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "  ", 1, 20);

        Assert.Equal("HIGH", blank.Items[0].AssetCode);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_SortByAge_OrdersByAgeDescendingNullsLast()
    {
        SetupRows(
            Row("NODATE", CatComputer, null, tickets: 6),
            Row("YOUNG", CatComputer, Today.AddYears(-6)),
            Row("OLD", CatComputer, Today.AddYears(-9)));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "age", 1, 20);

        Assert.Equal(new[] { "OLD", "YOUNG", "NODATE" }, result.Items.Select(i => i.AssetCode));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_SortByCode_OrdersByAssetCodeAscending()
    {
        SetupRows(
            Row("C", CatComputer, Today.AddYears(-6)),
            Row("A", CatComputer, Today.AddYears(-6)),
            Row("B", CatComputer, Today.AddYears(-6)));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, "code", 1, 20);

        Assert.Equal(new[] { "A", "B", "C" }, result.Items.Select(i => i.AssetCode));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_InvalidSortBy_ThrowsBeforeQueryingData()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GetCurrentReplacementCandidatesAsync(null, null, "Score; DROP", 1, 20));

        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_AssetUnderWarranty_StillIncludedWithIsUnderWarrantyTrue()
    {
        // L6: bảo hành chỉ là thông tin, không loại tài sản khỏi đề xuất và không đổi DueYear.
        SetupRows(
            Row("W-ACTIVE", CatComputer, Today.AddYears(-6), warranty: Today.AddMonths(6)),
            Row("W-TODAY", CatComputer, Today.AddYears(-6), warranty: Today),
            Row("W-EXPIRED", CatComputer, Today.AddYears(-6), warranty: Today.AddDays(-1)),
            Row("W-NONE", CatComputer, Today.AddYears(-6)));

        var items = (await _sut.GetCurrentReplacementCandidatesAsync(null, null, "code", 1, 20)).Items;

        Assert.Equal(4, items.Count);
        Assert.True(items.Single(i => i.AssetCode == "W-ACTIVE").IsUnderWarranty);
        Assert.True(items.Single(i => i.AssetCode == "W-TODAY").IsUnderWarranty);       // hết hạn đúng hôm nay vẫn còn hạn
        Assert.False(items.Single(i => i.AssetCode == "W-EXPIRED").IsUnderWarranty);
        Assert.False(items.Single(i => i.AssetCode == "W-NONE").IsUnderWarranty);
        Assert.All(items, i => Assert.Equal(Year, i.DueYear));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_PopulatesAppliedThresholdsFromPolicyOrDefault()
    {
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 4 });
        SetupRows(
            Row("PC", CatComputer, Today.AddYears(-6)),
            Row("SW", CatSwitch, Today.AddYears(-8)));

        var items = (await _sut.GetCurrentReplacementCandidatesAsync(null, null, "code", 1, 20)).Items;

        var pc = items.Single(i => i.AssetCode == "PC");
        var sw = items.Single(i => i.AssetCode == "SW");
        Assert.Equal((5, 3), (pc.AppliedMaxAgeYears, pc.AppliedMaxFailureCount));
        Assert.Equal((7, 4), (sw.AppliedMaxAgeYears, sw.AppliedMaxFailureCount));
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_ScenarioC22_ScoresAndReasonTextsMatchPlan()
    {
        SetupRows(
            Row("S12", CatComputer, Today.AddYears(-6), tickets: 1, warranty: Today.AddMonths(6), departmentId: 3),
            Row("S13", CatComputer, Today.AddYears(-2), tickets: 6, departmentId: 3));

        var items = (await _sut.GetCurrentReplacementCandidatesAsync(3, null, "score", 1, 20)).Items;

        Assert.Equal(new[] { "S13", "S12" }, items.Select(i => i.AssetCode));
        Assert.Equal(100, items[0].PriorityScore);
        Assert.Equal("Lỗi nhiều: 6 phiếu bảo trì (ngưỡng 3 phiếu)", items[0].ReasonText);
        Assert.InRange(items[1].PriorityScore, 59, 61);
        Assert.Matches(@"^Quá tuổi: 6,0 năm \(ngưỡng 5 năm\)$", items[1].ReasonText);
        Assert.True(items[1].IsUnderWarranty);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_BothReasons_ReasonTextJoinedAgeFirst()
    {
        SetupRows(Row("BOTH", CatComputer, Today.AddYears(-8), tickets: 4));

        var item = Assert.Single((await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20)).Items);

        Assert.StartsWith("Quá tuổi: ", item.ReasonText);
        Assert.EndsWith("; Lỗi nhiều: 4 phiếu bảo trì (ngưỡng 3 phiếu)", item.ReasonText);
        Assert.True(item.IsHighPriority);
    }

    // ---------- Tuần 8: sắp tới hạn ----------

    [Fact]
    public async Task GetUpcomingAsync_ExcludesAssetsAlreadyOverdue()
    {
        SetupRows(
            Row("OVER-AGE", CatComputer, Today.AddYears(-6)),
            Row("OVER-FAIL", CatComputer, Today.AddYears(-1), tickets: 3),
            Row("SOON", CatComputer, Today.AddYears(-5).AddMonths(2)));

        var result = await _sut.GetUpcomingAsync(null, null, 12, 1, 20);

        Assert.Equal("SOON", Assert.Single(result.Items).AssetCode);
    }

    [Fact]
    public async Task GetUpcomingAsync_SortedByDaysUntilAgeDueThenCode_NullsLast()
    {
        SetupRows(
            Row("FAIL-B", CatComputer, null, tickets: 2),                                   // chỉ sát ngưỡng lỗi → days null → cuối
            Row("FAIL-A", CatComputer, null, tickets: 2),
            Row("FAR", CatComputer, Today.AddYears(-5).AddMonths(8)),                       // đủ tuổi sau ~8 tháng
            Row("NEAR-B", CatComputer, Today.AddYears(-5).AddMonths(2)),                    // đủ tuổi sau ~2 tháng
            Row("NEAR-A", CatComputer, Today.AddYears(-5).AddMonths(2)));                   // cùng ngày → theo mã

        var result = await _sut.GetUpcomingAsync(null, null, 12, 1, 20);

        Assert.Equal(new[] { "NEAR-A", "NEAR-B", "FAR", "FAIL-A", "FAIL-B" }, result.Items.Select(i => i.AssetCode));
        Assert.All(result.Items.Where(i => i.AssetCode.StartsWith("FAIL")), i => Assert.Null(i.DaysUntilAgeDue));
    }

    [Fact]
    public async Task GetUpcomingAsync_CategoryFilterApplied()
    {
        SetupRows(
            Row("PC", CatComputer, Today.AddYears(-5).AddMonths(2)),
            Row("SW", CatSwitch, Today.AddYears(-5).AddMonths(2)));
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 5, MaxFailureCount = 3 });

        var result = await _sut.GetUpcomingAsync(null, CatSwitch, 12, 1, 20);

        Assert.Equal("SW", Assert.Single(result.Items).AssetCode);
    }

    [Fact]
    public async Task GetUpcomingAsync_MonthsWindowControlsWhoAppears()
    {
        SetupRows(Row("A", CatComputer, Today.AddYears(-5).AddMonths(6)));   // đủ tuổi sau ~6 tháng

        Assert.Empty((await _sut.GetUpcomingAsync(null, null, 3, 1, 20)).Items);
        Assert.Single((await _sut.GetUpcomingAsync(null, null, 6, 1, 20)).Items);
    }

    [Fact]
    public async Task GetUpcomingAsync_ScenarioC22_ReasonsAndTexts()
    {
        SetupRows(
            Row("S10", CatComputer, Today.AddMonths(-54), departmentId: 3),
            Row("S11", CatComputer, Today.AddMonths(-13), tickets: 2, departmentId: 3));

        var items = (await _sut.GetUpcomingAsync(3, null, 12, 1, 20)).Items;

        Assert.Equal(new[] { "S10", "S11" }, items.Select(i => i.AssetCode));
        Assert.Equal(new[] { LifecycleCalculator.ReasonAgeDueSoon }, items[0].Reasons);
        Assert.InRange(items[0].DaysUntilAgeDue!.Value, 175, 190);
        Assert.Equal(new[] { LifecycleCalculator.ReasonNearFailureThreshold }, items[1].Reasons);
        Assert.Null(items[1].DaysUntilAgeDue);
        Assert.Equal("Sát ngưỡng lỗi: 2/3 phiếu bảo trì", items[1].ReasonText);
    }

    [Fact]
    public async Task GetUpcomingAsync_NothingUpcoming_ReturnsEmptyPage()
    {
        SetupRows(Row("A", CatComputer, Today.AddYears(-1)));

        var result = await _sut.GetUpcomingAsync(null, null, 12, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task GetUpcomingAsync_PaginatesAndNormalizesPaging()
    {
        SetupRows(Enumerable.Range(1, 25).Select(i => Row($"U{i:D2}", CatComputer, null, tickets: 2)).ToArray());

        var page2 = await _sut.GetUpcomingAsync(null, null, 12, 2, 20);
        var bad = await _sut.GetUpcomingAsync(null, null, 12, 0, 5000);

        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(25, page2.TotalItems);
        Assert.Equal((1, 20), (bad.Page, bad.PageSize));
    }

    // ---------- Tuần 8: thống kê mở rộng ----------

    [Fact]
    public async Task GetStatsAsync_NoAssets_AllCollectionsEmptyAndNoException()
    {
        var stats = await _sut.GetStatsAsync(null);

        Assert.Equal(0, stats.TotalAssets);
        Assert.Null(stats.AverageAgeYears);
        Assert.Null(stats.MedianAgeYears);
        Assert.Empty(stats.ByCategory);
        Assert.Empty(stats.AgeBuckets);
        Assert.Empty(stats.ByDepartment);
        Assert.Empty(stats.TopFailingAssets);
        Assert.Empty(stats.TicketTrend);
    }

    [Fact]
    public async Task GetStatsAsync_AgeBuckets_HaveFixedLabelsInOrderAndEmptyBucketIsZeroNotNaN()
    {
        SetupRows(Row("A1", CatComputer, Today.AddYears(-2), tickets: 3));

        var buckets = (await _sut.GetStatsAsync(null)).AgeBuckets;

        Assert.Equal(new[] { "< 1 năm", "1–3 năm", "3–5 năm", "≥ 5 năm", "Không rõ ngày mua" }, buckets.Select(b => b.Label));
        foreach (var empty in buckets.Where(b => b.Count == 0))
        {
            Assert.Equal(0d, empty.FailureRatio);
            Assert.Equal(0d, empty.AverageTicketsPerAsset);
            Assert.False(double.IsNaN(empty.FailureRatio) || double.IsNaN(empty.AverageTicketsPerAsset));
        }

        var one = buckets[1];
        Assert.Equal((1, 1, 1d, 3d), (one.Count, one.AssetsWithAtLeastOneTicket, one.FailureRatio, one.AverageTicketsPerAsset));
    }

    [Fact]
    public async Task GetStatsAsync_AgeBucketsScenarioC22_FailureRatiosPerBucket()
    {
        SetupRows(
            Row("S10", CatComputer, Today.AddMonths(-54)),
            Row("S11", CatComputer, Today.AddMonths(-13), tickets: 2),
            Row("S12", CatComputer, Today.AddYears(-6), tickets: 1),
            Row("S13", CatComputer, Today.AddYears(-2), tickets: 6));

        var stats = await _sut.GetStatsAsync(null);
        var b = stats.AgeBuckets;

        Assert.Equal((2, 2, 1d, 4d), (b[1].Count, b[1].AssetsWithAtLeastOneTicket, b[1].FailureRatio, b[1].AverageTicketsPerAsset));
        Assert.Equal((1, 0, 0d, 0d), (b[2].Count, b[2].AssetsWithAtLeastOneTicket, b[2].FailureRatio, b[2].AverageTicketsPerAsset));
        Assert.Equal((1, 1, 1d, 1d), (b[3].Count, b[3].AssetsWithAtLeastOneTicket, b[3].FailureRatio, b[3].AverageTicketsPerAsset));
        Assert.Equal(4, b.Sum(x => x.Count));
        Assert.Equal(0.75, stats.FailureRatio);
        Assert.Equal(2.25, stats.AverageTicketsPerAsset);
        Assert.Equal(2, stats.OverdueNowCount);
    }

    [Fact]
    public async Task GetStatsAsync_MedianAge_EvenCountAveragesMiddleTwo()
    {
        SetupRows(
            Row("A", CatComputer, Today.AddYears(-1)),
            Row("B", CatComputer, Today.AddYears(-2)),
            Row("C", CatComputer, Today.AddYears(-4)),
            Row("D", CatComputer, Today.AddYears(-9)));

        var stats = await _sut.GetStatsAsync(null);

        Assert.InRange(stats.MedianAgeYears!.Value, 2.95, 3.05);       // (2 + 4) / 2
        Assert.InRange(stats.AverageAgeYears!.Value, 3.95, 4.05);      // trung bình bị kéo bởi tài sản 9 năm
    }

    [Fact]
    public async Task GetStatsAsync_MedianAge_OddCountTakesMiddleValue()
    {
        SetupRows(
            Row("A", CatComputer, Today.AddYears(-1)),
            Row("B", CatComputer, Today.AddYears(-3)),
            Row("C", CatComputer, Today.AddYears(-20)));

        Assert.InRange((await _sut.GetStatsAsync(null)).MedianAgeYears!.Value, 2.98, 3.02);
    }

    [Fact]
    public async Task GetStatsAsync_MedianAge_NoAssetsWithPurchaseDate_IsNull()
    {
        SetupRows(Row("A", CatComputer, null, tickets: 1));

        var stats = await _sut.GetStatsAsync(null);

        Assert.Null(stats.MedianAgeYears);
        Assert.Null(stats.AverageAgeYears);
        Assert.Equal(1, stats.AssetsWithoutPurchaseDate);
        Assert.Equal(5, stats.AgeBuckets.Count);
        Assert.Equal(1, stats.AgeBuckets[4].Count);
    }

    [Fact]
    public async Task GetStatsAsync_ByDepartment_OnlyDepartmentsWithAssetsSortedByName()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-6), tickets: 1, departmentId: 2, departmentName: "IT-B"),
            Row("A2", CatComputer, Today.AddYears(-2), departmentId: 2, departmentName: "IT-B"),
            Row("C1", CatComputer, Today.AddYears(-1), departmentId: 3, departmentName: "IT-C"),
            Row("Z1", CatComputer, null, tickets: 3, departmentId: 1, departmentName: "IT-A"));

        var byDept = (await _sut.GetStatsAsync(null)).ByDepartment;

        Assert.Equal(new[] { "IT-A", "IT-B", "IT-C" }, byDept.Select(d => d.DepartmentName));
        var b = byDept.Single(d => d.DepartmentId == 2);
        Assert.Equal((2, 1, 0.5, 1), (b.AssetCount, b.AssetsWithAtLeastOneTicket, b.FailureRatio, b.OverdueNowCount));
        Assert.InRange(b.AverageAgeYears!.Value, 3.95, 4.05);
        var a = byDept.Single(d => d.DepartmentId == 1);
        Assert.Null(a.AverageAgeYears);
        Assert.Equal(1, a.OverdueNowCount);   // 3 phiếu ≥ ngưỡng 3
    }

    [Fact]
    public async Task GetStatsAsync_DepartmentFilter_ByDepartmentContainsOnlyThatDepartment()
    {
        // Repository đã lọc theo phòng ban; Service phải truyền đúng departmentId và chỉ ghép những gì repository trả về.
        _repo.Setup(r => r.GetAssetLifecycleRowsAsync(3)).ReturnsAsync(new List<AssetLifecycleRow>
        {
            Row("C1", CatComputer, Today.AddYears(-1), departmentId: 3, departmentName: "IT-C"),
        });

        var stats = await _sut.GetStatsAsync(3);

        Assert.Equal("IT-C", Assert.Single(stats.ByDepartment).DepartmentName);
        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(3), Times.Once);
        _repo.Verify(r => r.GetTicketCountByYearAsync(3, Year - 4), Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_TopFailingAssets_AtMostTenOrderedByTicketCountThenFailedCountThenCode()
    {
        var rows = Enumerable.Range(1, 12)
            .Select(i => Row($"T{i:D2}", CatComputer, Today.AddYears(-1), tickets: i, failed: 0))
            .Concat(new[]
            {
                Row("TIE-B", CatComputer, Today.AddYears(-1), tickets: 12, failed: 2),
                Row("TIE-A", CatComputer, Today.AddYears(-1), tickets: 12, failed: 2),
                Row("TIE-F", CatComputer, Today.AddYears(-1), tickets: 12, failed: 5),
            }).ToArray();
        SetupRows(rows);

        var top = (await _sut.GetStatsAsync(null)).TopFailingAssets;

        Assert.Equal(10, top.Count);
        Assert.Equal(new[] { "TIE-F", "TIE-A", "TIE-B", "T12" }, top.Take(4).Select(t => t.AssetCode));
        Assert.All(top, t => Assert.NotNull(t.AgeYears));
    }

    [Fact]
    public async Task GetStatsAsync_TopFailingAssets_ExcludesAssetsWithZeroTickets()
    {
        SetupRows(
            Row("NONE", CatComputer, Today.AddYears(-1)),
            Row("ONE", CatComputer, Today.AddYears(-1), tickets: 1));

        var top = (await _sut.GetStatsAsync(null)).TopFailingAssets;

        Assert.Equal("ONE", Assert.Single(top).AssetCode);
    }

    [Fact]
    public async Task GetStatsAsync_FixedNumberOfRepositoryCalls_NoPerAssetQueries()
    {
        SetupRows(Enumerable.Range(1, 200)
            .Select(i => Row($"A{i:D3}", i % 2 == 0 ? CatComputer : CatSwitch, Today.AddYears(-(i % 9)), tickets: i % 5, departmentId: 1 + i % 3))
            .ToArray());

        await _sut.GetStatsAsync(null);

        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>()), Times.Once);
        _repo.Verify(r => r.GetPoliciesAsync(), Times.Once);
        _repo.Verify(r => r.GetTicketCountByYearAsync(It.IsAny<int?>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetUpcomingAsync_FixedNumberOfRepositoryCalls()
    {
        SetupRows(Enumerable.Range(1, 50).Select(i => Row($"A{i:D2}", CatComputer, null, tickets: 2)).ToArray());

        await _sut.GetUpcomingAsync(null, null, 12, 1, 20);

        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>()), Times.Once);
        _repo.Verify(r => r.GetPoliciesAsync(), Times.Once);
    }

    // ---------- M2: chi tiết vòng đời một tài sản ----------

    private void SetupTickets(int assetId, params AssetTicketSummaryDto[] tickets) =>
        _repo.Setup(r => r.GetTicketsByAssetAsync(assetId)).ReturnsAsync(tickets.ToList());

    private void SetupSingleRow(AssetLifecycleRow row) =>
        _repo.Setup(r => r.GetAssetLifecycleRowAsync(row.AssetId)).ReturnsAsync(row);

    private static AssetTicketSummaryDto Ticket(int id, DateTime reported, string status = "Resolved", string description = "x") => new()
    {
        Id = id, ReportedDate = reported, Status = status, Description = description,
    };

    [Fact]
    public async Task GetAssetDetailAsync_AssetNotFoundOrDisposed_ThrowsLifecycleAssetNotFoundException()
    {
        _repo.Setup(r => r.GetAssetLifecycleRowAsync(99)).ReturnsAsync((AssetLifecycleRow?)null);

        await Assert.ThrowsAsync<LifecycleAssetNotFoundException>(() => _sut.GetAssetDetailAsync(99));
        _repo.Verify(r => r.GetTicketsByAssetAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAssetDetailAsync_NotYetOverdue_ReturnsDetailWithIsOverdueNowFalse()
    {
        var row = Row("A1", CatComputer, Today.AddYears(-2), tickets: 1);
        row.AssetId = 7;
        SetupSingleRow(row);
        SetupTickets(7);

        var detail = await _sut.GetAssetDetailAsync(7);

        Assert.False(detail.IsOverdueNow);
        Assert.False(detail.IsHighPriority);
        Assert.Equal(Year + 3, detail.DueYear);
        Assert.Empty(detail.Reasons);
        Assert.Equal(string.Empty, detail.ReasonText);
        Assert.Equal(Today.AddYears(-2).AddYears(5), detail.AgeDueDate);
        Assert.Equal((5, 3), (detail.AppliedMaxAgeYears, detail.AppliedMaxFailureCount));
        Assert.InRange(detail.PriorityScore, 16, 21);   // max(2/5, 1/3) = 0,4 → 20
    }

    [Fact]
    public async Task GetAssetDetailAsync_OverdueAsset_MatchesListRecommendationExactly()
    {
        // Cùng một đường quyết định: chi tiết phải trùng điểm, lý do, ngưỡng với dòng tương ứng trong danh sách đề xuất.
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 4 });
        var row = Row("SW", CatSwitch, Today.AddYears(-8), tickets: 4, warranty: Today.AddMonths(2));
        row.AssetId = 11;
        SetupRows(row);
        SetupSingleRow(row);
        SetupTickets(11);

        var listItem = Assert.Single((await _sut.GetCurrentReplacementCandidatesAsync(null, null, "score", 1, 20)).Items);
        var detail = await _sut.GetAssetDetailAsync(11);

        Assert.Equal(
            (listItem.PriorityScore, listItem.ReasonText, listItem.IsOverdueNow, listItem.IsHighPriority, (int?)listItem.DueYear,
             listItem.AppliedMaxAgeYears, listItem.AppliedMaxFailureCount, listItem.IsUnderWarranty),
            (detail.PriorityScore, detail.ReasonText, detail.IsOverdueNow, detail.IsHighPriority, detail.DueYear,
             detail.AppliedMaxAgeYears, detail.AppliedMaxFailureCount, detail.IsUnderWarranty));
        Assert.Equal(listItem.Reasons, detail.Reasons);
        Assert.Equal((7, 4), (detail.AppliedMaxAgeYears, detail.AppliedMaxFailureCount));
    }

    [Fact]
    public async Task GetAssetDetailAsync_NoPurchaseDateAndFewTickets_HasNullDueYearAndNullAgeDueDate()
    {
        var row = Row("A1", CatComputer, null, tickets: 1);
        row.AssetId = 5;
        SetupSingleRow(row);
        SetupTickets(5);

        var detail = await _sut.GetAssetDetailAsync(5);

        Assert.Null(detail.DueYear);
        Assert.Null(detail.AgeDueDate);
        Assert.Null(detail.AgeYears);
        Assert.False(detail.IsOverdueNow);
    }

    [Fact]
    public async Task GetAssetDetailAsync_TicketsKeptInRepositoryOrderAndDescriptionCutTo200()
    {
        var row = Row("A1", CatComputer, Today.AddYears(-1), tickets: 3);
        row.AssetId = 8;
        SetupSingleRow(row);
        SetupTickets(8,
            Ticket(3, new DateTime(2026, 9, 1), "Pending", new string('x', 500)),
            Ticket(2, new DateTime(2026, 5, 1), "Failed", new string('y', 200)),
            Ticket(1, new DateTime(2025, 1, 1), "Resolved", "ngắn"));

        var detail = await _sut.GetAssetDetailAsync(8);

        Assert.Equal(new[] { 3, 2, 1 }, detail.Tickets.Select(t => t.Id));
        Assert.Equal(200, detail.Tickets[0].Description.Length);
        Assert.Equal(200, detail.Tickets[1].Description.Length);   // đúng 200 không bị cắt thêm
        Assert.Equal("ngắn", detail.Tickets[2].Description);
        Assert.Equal(new[] { "Pending", "Failed", "Resolved" }, detail.Tickets.Select(t => t.Status));
    }

    [Fact]
    public async Task GetAssetDetailAsync_AssetWithoutTickets_ReturnsEmptyList()
    {
        var row = Row("A1", CatComputer, Today.AddYears(-1));
        row.AssetId = 9;
        SetupSingleRow(row);
        SetupTickets(9);

        Assert.Empty((await _sut.GetAssetDetailAsync(9)).Tickets);
    }

    [Fact]
    public async Task GetAssetDetailAsync_FixedNumberOfRepositoryCalls()
    {
        var row = Row("A1", CatComputer, Today.AddYears(-6), tickets: 2);
        row.AssetId = 10;
        SetupSingleRow(row);
        SetupTickets(10);

        await _sut.GetAssetDetailAsync(10);

        _repo.Verify(r => r.GetAssetLifecycleRowAsync(10), Times.Once);
        _repo.Verify(r => r.GetTicketsByAssetAsync(10), Times.Once);
        _repo.Verify(r => r.GetPoliciesAsync(), Times.Once);
        _repo.Verify(r => r.GetAssetLifecycleRowsAsync(It.IsAny<int?>()), Times.Never);   // không nạp cả danh sách
    }

    // ---------- Chính sách ----------

    [Fact]
    public async Task UpsertPolicyAsync_CategoryNotFound_ThrowsAssetCategoryNotFoundException()
    {
        _repo.Setup(r => r.GetCategoryByIdAsync(99)).ReturnsAsync((AssetCategory?)null);

        await Assert.ThrowsAsync<AssetCategoryNotFoundException>(() =>
            _sut.UpsertPolicyAsync(99, new UpsertLifecyclePolicyRequestDto { MaxAgeYears = 5, MaxFailureCount = 3 }));
    }

    [Fact]
    public async Task UpsertPolicyAsync_NewPolicy_AddsAndSaves()
    {
        _repo.Setup(r => r.GetCategoryByIdAsync(CatComputer)).ReturnsAsync(new AssetCategory { Id = CatComputer, Name = "Máy tính" });
        _repo.Setup(r => r.GetPolicyByCategoryIdAsync(CatComputer)).ReturnsAsync((AssetCategoryLifecyclePolicy?)null);

        var result = await _sut.UpsertPolicyAsync(CatComputer, new UpsertLifecyclePolicyRequestDto { MaxAgeYears = 4, MaxFailureCount = 2 });

        _repo.Verify(r => r.AddPolicyAsync(It.Is<AssetCategoryLifecyclePolicy>(
            p => p.CategoryId == CatComputer && p.MaxAgeYears == 4 && p.MaxFailureCount == 2)), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        Assert.True(result.IsOverridden);
    }

    [Fact]
    public async Task UpsertPolicyAsync_ExistingPolicy_UpdatesInsteadOfAdding()
    {
        var existing = new AssetCategoryLifecyclePolicy { Id = 1, CategoryId = CatComputer, MaxAgeYears = 5, MaxFailureCount = 3 };
        _repo.Setup(r => r.GetCategoryByIdAsync(CatComputer)).ReturnsAsync(new AssetCategory { Id = CatComputer, Name = "Máy tính" });
        _repo.Setup(r => r.GetPolicyByCategoryIdAsync(CatComputer)).ReturnsAsync(existing);

        var result = await _sut.UpsertPolicyAsync(CatComputer, new UpsertLifecyclePolicyRequestDto { MaxAgeYears = 4, MaxFailureCount = 2 });

        _repo.Verify(r => r.AddPolicyAsync(It.IsAny<AssetCategoryLifecyclePolicy>()), Times.Never);
        Assert.Equal((4, 2), (existing.MaxAgeYears, existing.MaxFailureCount));
        Assert.Equal((4, 2), (result.MaxAgeYears, result.MaxFailureCount));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(31, 3)]
    [InlineData(5, 0)]
    [InlineData(5, 101)]
    public async Task UpsertPolicyAsync_OutOfRange_ThrowsArgumentExceptionWithoutSaving(int age, int failures)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpsertPolicyAsync(CatComputer, new UpsertLifecyclePolicyRequestDto { MaxAgeYears = age, MaxFailureCount = failures }));

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeletePolicyAsync_NoOverride_ThrowsLifecyclePolicyNotFoundException()
    {
        _repo.Setup(r => r.GetPolicyByCategoryIdAsync(CatComputer)).ReturnsAsync((AssetCategoryLifecyclePolicy?)null);

        await Assert.ThrowsAsync<LifecyclePolicyNotFoundException>(() => _sut.DeletePolicyAsync(CatComputer));
    }

    [Fact]
    public async Task DeletePolicyAsync_ExistingPolicy_RemovesAndSaves()
    {
        var existing = new AssetCategoryLifecyclePolicy { Id = 1, CategoryId = CatComputer, MaxAgeYears = 5, MaxFailureCount = 3 };
        _repo.Setup(r => r.GetPolicyByCategoryIdAsync(CatComputer)).ReturnsAsync(existing);

        await _sut.DeletePolicyAsync(CatComputer);

        _repo.Verify(r => r.RemovePolicy(existing), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetPoliciesAsync_CategoryWithoutOverride_ReturnsDefaultsWithIsOverriddenFalse()
    {
        SetupPolicies(new AssetCategoryLifecyclePolicy { CategoryId = CatSwitch, MaxAgeYears = 7, MaxFailureCount = 3 });

        var result = await _sut.GetPoliciesAsync();

        var computer = result.Single(p => p.CategoryId == CatComputer);
        Assert.False(computer.IsOverridden);
        Assert.Equal((5, 3), (computer.MaxAgeYears, computer.MaxFailureCount));
        Assert.True(result.Single(p => p.CategoryId == CatSwitch).IsOverridden);
    }
}
