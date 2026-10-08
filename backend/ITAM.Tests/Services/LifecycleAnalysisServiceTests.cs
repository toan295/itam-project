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
        string code, int categoryId, DateOnly? purchase, int tickets = 0, int failed = 0, int departmentId = 1) => new()
    {
        AssetId = Math.Abs(code.GetHashCode()),
        AssetCode = code,
        AssetName = code,
        CategoryId = categoryId,
        CategoryName = categoryId == CatComputer ? "Máy tính" : "Switch",
        DepartmentId = departmentId,
        DepartmentName = "IT-A",
        PurchaseDate = purchase,
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

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, 1, 20);

        Assert.Equal("A1", Assert.Single(result.Items).AssetCode);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_CategoryFilterApplied()
    {
        SetupRows(
            Row("A1", CatComputer, Today.AddYears(-6)),
            Row("S1", CatSwitch, Today.AddYears(-6)));

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, CatSwitch, 1, 20);

        Assert.Equal("S1", Assert.Single(result.Items).AssetCode);
    }

    [Fact]
    public async Task GetCurrentReplacementCandidatesAsync_PaginatesAndNormalizesPaging()
    {
        SetupRows(Enumerable.Range(1, 130).Select(i => Row($"A{i:D3}", CatComputer, Today.AddYears(-6))).ToArray());

        var page1 = await _sut.GetCurrentReplacementCandidatesAsync(null, null, 1, 100);
        var page2 = await _sut.GetCurrentReplacementCandidatesAsync(null, null, 2, 100);
        var outOfRange = await _sut.GetCurrentReplacementCandidatesAsync(null, null, int.MaxValue, 5000);   // không được tràn số / lỗi

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

        var result = await _sut.GetCurrentReplacementCandidatesAsync(null, null, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
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
