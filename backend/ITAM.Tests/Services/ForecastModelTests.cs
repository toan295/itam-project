using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

// Mô hình mua thêm có xét nhân sự hiện tại + tồn kho dùng được, thay thế độc lập với tồn kho, tái lập và phát hiện dự báo cũ.
public class ForecastModelTests
{
    private const int Dept = 1;
    private const int CatPc = 10;
    private const int CatPrinter = 11;

    private static readonly int Year = VietnamTime.Today().Year;

    private readonly Mock<IBudgetForecastRepository> _repo = new();
    private readonly Mock<ILifecycleAnalysisService> _lifecycle = new();
    private readonly Mock<IExclusiveSection> _exclusive = new();
    private readonly BudgetForecastService _sut;

    private int _baseline;
    private int _planDelta;
    private bool _hasPlan;
    private readonly Dictionary<(int, int), int> _supply = new();
    private readonly List<AssetCategoryDeviceRatio> _ratios = new();
    private readonly Dictionary<int, decimal> _prices = new() { [CatPc] = 20_000_000m, [CatPrinter] = 4_000_000m };
    private readonly List<AssetReplacementCandidateDto> _candidates = new();
    private readonly List<BudgetForecast> _existing = new();
    private readonly Dictionary<int, int> _noDate = new();

    public ForecastModelTests()
    {
        _exclusive.Setup(e => e.RunAsync(It.IsAny<LockTarget>(), It.IsAny<int>(), It.IsAny<Func<Task<ForecastGenerationResultDto>>>()))
            .Returns((LockTarget _, int _, Func<Task<ForecastGenerationResultDto>> work) => work());
        _sut = new BudgetForecastService(_repo.Object, _lifecycle.Object, _exclusive.Object, NullLogger<BudgetForecastService>.Instance);

        _repo.Setup(r => r.GetDepartmentAsync(Dept)).ReturnsAsync((Dept, "IT-A"));
        _repo.Setup(r => r.GetAllDepartmentsAsync()).ReturnsAsync(new List<(int Id, string Name)> { (Dept, "IT-A") });
        _repo.Setup(r => r.GetAllCategoriesAsync()).ReturnsAsync(new List<(int Id, string Name)> { (CatPc, "Máy tính"), (CatPrinter, "Máy in") });
        _repo.Setup(r => r.GetAllPricesAsync()).ReturnsAsync(() => _prices.Select(p => new AssetCategoryReferencePrice { CategoryId = p.Key, UnitPrice = p.Value }).ToList());
        _repo.Setup(r => r.GetAllRatiosAsync()).ReturnsAsync(() => _ratios.ToList());
        _repo.Setup(r => r.GetPlansAsync(It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(() => _hasPlan
            ? new List<DepartmentExpansionPlan> { new() { Year = Year, DepartmentId = Dept, AdditionalHeadcount = _planDelta } }
            : new List<DepartmentExpansionPlan>());
        _repo.Setup(r => r.GetActiveEmployeeCountsAsync()).ReturnsAsync(() => new Dictionary<int, int> { [Dept] = _baseline });
        _repo.Setup(r => r.GetUsableAssetCountsAsync()).ReturnsAsync(() => new Dictionary<(int DepartmentId, int CategoryId), int>(_supply.Select(kv => new KeyValuePair<(int DepartmentId, int CategoryId), int>(kv.Key, kv.Value))));
        _repo.Setup(r => r.GetAssetsWithoutPurchaseDateCountsAsync()).ReturnsAsync(() => new Dictionary<int, int>(_noDate));
        _repo.Setup(r => r.GetForecastsByYearAsync(It.IsAny<int>())).ReturnsAsync(() => _existing.ToList());
        _repo.Setup(r => r.AddAsync(It.IsAny<BudgetForecast>())).Callback<BudgetForecast>(f => { f.Id = 100; _existing.Add(f); }).Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetUserNamesAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, string>());
        _lifecycle.Setup(l => l.GetReplacementCandidatesAsync(It.IsAny<int?>(), It.IsAny<int>())).ReturnsAsync(() => _candidates.ToList());
    }

    private void Ratio(int category, decimal value) => _ratios.Add(new AssetCategoryDeviceRatio { CategoryId = category, DevicesPerPerson = value });

    private void Due(int category, string name, int count, int dueYear)
    {
        for (var i = 0; i < count; i++)
        {
            _candidates.Add(new AssetReplacementCandidateDto
            {
                AssetId = _candidates.Count + 1, AssetCode = $"A-{_candidates.Count + 1:000}", AssetName = "x", CategoryId = category,
                CategoryName = name, DepartmentId = Dept, DepartmentName = "IT-A", DueYear = dueYear,
            });
        }
    }

    private async Task<(BudgetForecastResponseDto Row, ForecastGenerationResultDto Result)> RunAsync(int? year = null)
    {
        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = year ?? Year, DepartmentId = Dept });
        return (Assert.Single(result.Forecasts), result);
    }

    // ---------- Mua thêm = phần thiếu hụt sau khi xét tồn kho ----------

    [Fact]
    public async Task Expansion_TenPeopleOnePerPerson_EightDevicesTwoMorePeople_BuysFourNotTwo()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 2;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 8;

        var (row, _) = await RunAsync();

        var item = Assert.Single(row.ExpansionBreakdown);
        Assert.Equal((12, 8, 4), (item.Demand, item.ExistingUsable, item.Quantity));
        Assert.Equal(4, row.EstimatedExpansionCount);
        Assert.Equal(80_000_000m, row.EstimatedExpansionBudget);
        Assert.Equal((10, 2, 12), (row.BaselineHeadcount, row.AdditionalHeadcount, row.ProjectedHeadcount));
        Assert.Contains("thiếu 4", item.Explanation);
    }

    [Fact]
    public async Task Expansion_StockCoversGrowth_BuysNothingAndReportsSurplus()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 2;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 15;

        var (row, _) = await RunAsync();

        var item = Assert.Single(row.ExpansionBreakdown);
        Assert.Equal((0, 3), (item.Quantity, item.Surplus));
        Assert.Equal(0, row.EstimatedExpansionCount);
        Assert.Empty(row.PurchaseSummary);            // đủ tồn kho: không có dòng mua sắm
    }

    [Fact]
    public async Task Replacement_IsNeverReducedByStock_AndExpansionIsNotReducedByDueDevices()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 2;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 8;
        Due(CatPc, "Máy tính", 2, Year);              // 2 trong 8 máy đến hạn

        var (row, _) = await RunAsync();

        Assert.Equal(2, row.EstimatedReplacementCount);                 // thay thế đủ 2, không bị trừ bởi tồn kho
        Assert.Equal(4, Assert.Single(row.ExpansionBreakdown).Quantity); // mua thêm vẫn 12 - 8; 2 máy đến hạn thay 1:1 không đếm lần hai
        var summary = Assert.Single(row.PurchaseSummary);
        Assert.Equal((2, 4, 6), (summary.ReplacementQuantity, summary.ExpansionQuantity, summary.TotalQuantity));
        Assert.Equal(120_000_000m, row.TotalBudget);
        Assert.Contains("thay 1:1", Assert.Single(row.ExpansionBreakdown).Explanation);
    }

    [Fact]
    public async Task Expansion_HeadcountDecrease_ReducesDemandAndOnlySuggestsAvoidingReplacement()
    {
        _baseline = 10; _hasPlan = true; _planDelta = -3;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 8;
        Due(CatPc, "Máy tính", 2, Year);

        var (row, result) = await RunAsync();

        var item = Assert.Single(row.ExpansionBreakdown);
        Assert.Equal((7, 8, 0, 1, 1), (item.Demand, item.ExistingUsable, item.Quantity, item.Surplus, item.ReplacementPossiblyAvoidable));
        Assert.Equal(2, row.EstimatedReplacementCount);                  // KHÔNG tự trừ: quyết định thủ công
        Assert.Contains(result.Warnings, w => w.Contains("có thể không cần mua thay 1"));
        Assert.Equal(7, row.ProjectedHeadcount);
    }

    [Fact]
    public async Task Expansion_NetHeadcountBelowZero_IsClampedToZeroWithWarning()
    {
        _baseline = 2; _hasPlan = true; _planDelta = -5;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 1;

        var (row, result) = await RunAsync();

        Assert.Equal(0, row.ProjectedHeadcount);
        Assert.Equal(0, row.EstimatedExpansionCount);
        Assert.Contains(result.Warnings, w => w.Contains("< 0"));
    }

    [Fact]
    public async Task Expansion_SharedDeviceRatioRoundsUpOnTheDepartmentTotal()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 1;      // 11 người × 0,10 = 1,1 → ⌈⌉ = 2 máy in
        Ratio(CatPrinter, 0.10m);
        _supply[(Dept, CatPrinter)] = 1;

        var (row, _) = await RunAsync();

        var item = Assert.Single(row.ExpansionBreakdown);
        Assert.Equal((2, 1, 1), (item.Demand, item.ExistingUsable, item.Quantity));
    }

    [Fact]
    public async Task Expansion_NoPlanButBaselineAndShortage_StillComputesFromCurrentStaff()
    {
        _baseline = 10; _hasPlan = false;
        Ratio(CatPc, 1m);
        _supply[(Dept, CatPc)] = 8;

        var (row, _) = await RunAsync();

        Assert.Equal(2, Assert.Single(row.ExpansionBreakdown).Quantity);   // đang thiếu 2 dù không tăng nhân sự
        Assert.Equal(0, row.AdditionalHeadcount);
    }

    [Fact]
    public async Task Expansion_CategoryWithoutRatio_IsNotConsideredForPurchase()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 5;
        _supply[(Dept, CatPc)] = 0;

        var (row, _) = await RunAsync();

        Assert.Empty(row.ExpansionBreakdown);
    }

    [Fact]
    public async Task Expansion_MissingPriceForShortfall_IsCountedButNotBudgetedAndWarnedOnce()
    {
        _baseline = 10; _hasPlan = true; _planDelta = 2;
        _prices.Remove(CatPc);
        Ratio(CatPc, 1m);
        Due(CatPc, "Máy tính", 1, Year);

        var (row, result) = await RunAsync();

        Assert.Equal(0m, row.TotalBudget);
        Assert.Equal(1 + 12, row.TotalPurchaseCount);
        Assert.Single(result.Warnings, w => w.Contains("Máy tính") && w.Contains("13 thiết bị chưa có đơn giá"));
    }

    // ---------- Chỉ tính tài sản đến hạn trong đúng năm ----------

    [Fact]
    public async Task Replacement_OnlyDueYearEqualToForecastYearIsCounted_OverdueFromEarlierYearsOnlyWarned()
    {
        Due(CatPc, "Máy tính", 1, Year + 1);          // đúng năm
        Due(CatPc, "Máy tính", 3, Year);              // đã tới hạn từ trước (DueYear = năm hiện tại)

        var (row, result) = await RunAsync(Year + 1);

        Assert.Equal(1, row.EstimatedReplacementCount);
        Assert.Contains(result.Warnings, w => w.Contains("3 tài sản đã tới hạn thay thế trước năm") && w.Contains((Year + 1).ToString()));
    }

    [Fact]
    public async Task Replacement_CurrentYear_DoesNotWarnAboutCarryOver()
    {
        Due(CatPc, "Máy tính", 2, Year);

        var (row, result) = await RunAsync();

        Assert.Equal(2, row.EstimatedReplacementCount);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("trước năm"));
    }

    [Fact]
    public async Task Warnings_AssetsWithoutPurchaseDate_AreReportedPerDepartment()
    {
        _noDate[Dept] = 4;

        var (_, result) = await RunAsync();

        Assert.Contains(result.Warnings, w => w.Contains("4 tài sản chưa có ngày mua"));
    }

    // ---------- Tái lập, idempotent, đồng thời ----------

    [Fact]
    public async Task Generate_RunsInsideTheYearLock()
    {
        await RunAsync();

        _exclusive.Verify(e => e.RunAsync(LockTarget.ForecastYear, Year, It.IsAny<Func<Task<ForecastGenerationResultDto>>>()), Times.Once);
    }

    [Fact]
    public async Task Generate_SameInputsTwice_SecondRunIsUnchangedAndWritesNothing()
    {
        Ratio(CatPc, 1m); _baseline = 5; _supply[(Dept, CatPc)] = 3;
        Due(CatPc, "Máy tính", 1, Year);

        var (first, _) = await RunAsync();
        var stamp = _existing.Single().GeneratedAt;
        await Task.Delay(5);
        var (second, _) = await RunAsync();

        Assert.Equal("Created", first.Change);
        Assert.Equal("Unchanged", second.Change);
        Assert.Single(_existing);                                  // không sinh dòng trùng
        Assert.Equal(1, second.Revision);
        Assert.Equal(stamp, _existing.Single().GeneratedAt);        // không đổi GeneratedAt
        Assert.Equal(first.TotalBudget, second.TotalBudget);
    }

    [Fact]
    public async Task Generate_ChangedPrice_UpdatesRowBumpsRevisionAndReportsPrevious()
    {
        Due(CatPc, "Máy tính", 2, Year);
        var (first, _) = await RunAsync();

        _prices[CatPc] = 25_000_000m;
        var (second, _) = await RunAsync();

        Assert.Equal("Updated", second.Change);
        Assert.Equal(2, second.Revision);
        Assert.Equal(40_000_000m, second.Previous!.TotalBudget);
        Assert.Equal(50_000_000m, second.TotalBudget);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Generate_SoftDeletedRow_IsRestoredAndRevisionBumped()
    {
        Due(CatPc, "Máy tính", 1, Year);
        await RunAsync();
        _existing.Single().IsDeleted = true;

        var (row, _) = await RunAsync();

        Assert.Equal("Restored", row.Change);
        Assert.Null(row.Previous);
        Assert.False(_existing.Single().IsDeleted);
        Assert.Single(_existing);
    }

    [Fact]
    public async Task Generate_LoadsEveryInputOnceRegardlessOfDepartmentCount()
    {
        _repo.Setup(r => r.GetAllDepartmentsAsync()).ReturnsAsync(Enumerable.Range(1, 6).Select(i => (i, $"P{i}")).ToList());

        await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year });

        _repo.Verify(r => r.GetForecastsByYearAsync(Year), Times.Once);
        _repo.Verify(r => r.GetUsableAssetCountsAsync(), Times.Once);
        _repo.Verify(r => r.GetActiveEmployeeCountsAsync(), Times.Once);
        _repo.Verify(r => r.GetAllRatiosAsync(), Times.Once);
        _lifecycle.Verify(l => l.GetReplacementCandidatesAsync(It.IsAny<int?>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Generate_BudgetBeyondStorageLimit_IsRejectedBeforeAnyWrite()
    {
        _baseline = 5000; _hasPlan = true; _planDelta = 0;
        _prices[CatPc] = 1_000_000_000_000m;
        Ratio(CatPc, 20m);                             // 100.000 thiết bị × 1e12 = 1e17 > giới hạn decimal(18,2)

        await Assert.ThrowsAsync<ArgumentException>(() => RunAsync());

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Generate_SaveFails_NothingIsReportedAsSaved()
    {
        Due(CatPc, "Máy tính", 1, Year);
        _repo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("boom"));

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => RunAsync());
    }

    [Fact]
    public async Task Generate_YearIsJudgedOnVietnamCalendar()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year - 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year + 11 }));
    }

    // ---------- Dự báo cũ: giữ nguyên số liệu, giải thích vì sao khác ----------

    [Fact]
    public async Task GetById_AfterPriceChange_KeepsStoredNumbersAndExplainsStale()
    {
        Ratio(CatPc, 1m); _baseline = 4; _supply[(Dept, CatPc)] = 1;
        Due(CatPc, "Máy tính", 1, Year);
        var (created, _) = await RunAsync();
        var entity = _existing.Single();
        entity.Department = new Department { Id = Dept, Name = "IT-A" };
        _repo.Setup(r => r.GetByIdWithDetailsAsync(100)).ReturnsAsync(entity);

        var fresh = await _sut.GetByIdAsync(100);
        Assert.Equal("Current", fresh.StaleStatus);

        _prices[CatPc] = 30_000_000m;
        _supply[(Dept, CatPc)] = 3;
        _baseline = 6;
        var stale = await _sut.GetByIdAsync(100);

        Assert.Equal(created.TotalBudget, stale.TotalBudget);          // số liệu đã lưu KHÔNG đổi
        Assert.Equal("Stale", stale.StaleStatus);
        Assert.Contains(stale.StaleReasons, r => r.Contains("Đơn giá 'Máy tính'") && r.Contains("→"));
        Assert.Contains(stale.StaleReasons, r => r.Contains("Nhân viên hiện tại") && r.Contains("4 → 6"));
        Assert.Contains(stale.StaleReasons, r => r.Contains("Tồn kho dùng được 'Máy tính': 1 → 3"));
    }

    [Fact]
    public async Task GetById_AssetNoLongerDue_IsReportedAsStale()
    {
        Due(CatPc, "Máy tính", 2, Year);
        await RunAsync();
        var entity = _existing.Single();
        entity.Department = new Department { Id = Dept, Name = "IT-A" };
        _repo.Setup(r => r.GetByIdWithDetailsAsync(100)).ReturnsAsync(entity);

        _candidates.RemoveAt(0);
        var stale = await _sut.GetByIdAsync(100);

        Assert.Equal("Stale", stale.StaleStatus);
        Assert.Contains(stale.StaleReasons, r => r.Contains("1 tài sản không còn thuộc diện thay thế"));
    }

    [Fact]
    public async Task GetById_LegacyRowWithoutInputsSnapshot_IsUnknownNotError()
    {
        var legacy = new BudgetForecast
        {
            Id = 5, Year = Year, DepartmentId = Dept, EstimatedReplacementCount = 2, EstimatedBudget = 40_000_000m,
            Department = new Department { Id = Dept, Name = "IT-A" },
        };
        _repo.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(legacy);

        var result = await _sut.GetByIdAsync(5);

        Assert.Equal("Unknown", result.StaleStatus);
        Assert.Equal(40_000_000m, result.TotalBudget);
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public async Task GetById_CorruptExpansionJson_DoesNotThrow()
    {
        var broken = new BudgetForecast
        {
            Id = 6, Year = Year, DepartmentId = Dept, ExpansionBreakdownJson = "{not json", BreakdownJson = "[also bad",
            Department = new Department { Id = Dept, Name = "IT-A" },
        };
        _repo.Setup(r => r.GetByIdWithDetailsAsync(6)).ReturnsAsync(broken);

        var result = await _sut.GetByIdAsync(6);

        Assert.Empty(result.ExpansionBreakdown);
        Assert.Empty(result.Breakdown);
    }

    // ---------- Phiên bản đầu vào (hai Admin cùng sửa) ----------

    [Fact]
    public async Task UpsertPrice_ExistingWithStaleVersion_Conflicts()
    {
        _repo.Setup(r => r.GetPriceAsync(CatPc)).ReturnsAsync(new AssetCategoryReferencePrice { CategoryId = CatPc, UnitPrice = 1m, Version = 3 });

        await Assert.ThrowsAsync<ForecastInputConflictException>(() =>
            _sut.UpsertPriceAsync(CatPc, new UpsertReferencePriceRequestDto { UnitPrice = 2m, Version = 2 }));
        await Assert.ThrowsAsync<ForecastInputConflictException>(() =>
            _sut.UpsertPriceAsync(CatPc, new UpsertReferencePriceRequestDto { UnitPrice = 2m }));
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertPrice_DuplicateCreate_ConflictsInsteadOfFailing()
    {
        _repo.Setup(r => r.GetPriceAsync(CatPrinter)).ReturnsAsync((AssetCategoryReferencePrice?)null);
        _repo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("dup"));

        await Assert.ThrowsAsync<ForecastInputConflictException>(() =>
            _sut.UpsertPriceAsync(CatPrinter, new UpsertReferencePriceRequestDto { UnitPrice = 5_000_000m }));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(1.234)]
    public async Task UpsertPrice_MoreThanTwoDecimals_IsRejected(double price)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpsertPriceAsync(CatPc, new UpsertReferencePriceRequestDto { UnitPrice = (decimal)price }));
    }

    [Fact]
    public async Task DeletePrice_WithoutOrWithStaleVersion_IsRefusedAndKeepsPrice()
    {
        var existing = new AssetCategoryReferencePrice { CategoryId = CatPc, UnitPrice = 1m, Version = 4 };
        _repo.Setup(r => r.GetPriceAsync(CatPc)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.DeletePriceAsync(CatPc));
        await Assert.ThrowsAsync<ForecastInputConflictException>(() => _sut.DeletePriceAsync(CatPc, 3));

        Assert.False(existing.IsDeleted);
    }
}
