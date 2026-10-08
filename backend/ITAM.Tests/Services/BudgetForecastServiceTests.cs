using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class BudgetForecastServiceTests
{
    private const int DeptA = 1;
    private const int DeptB = 2;
    private const int CatComputer = 10;
    private const int CatPrinter = 11;
    private const int CatSwitch = 12;

    private static readonly int Year = DateTime.UtcNow.Year;

    private readonly Mock<IBudgetForecastRepository> _repo = new();
    private readonly Mock<ILifecycleAnalysisService> _lifecycle = new();
    private readonly BudgetForecastService _sut;

    private readonly List<(int Id, string Name)> _departments = new() { (DeptA, "IT-A"), (DeptB, "IT-B") };

    public BudgetForecastServiceTests()
    {
        _sut = new BudgetForecastService(_repo.Object, _lifecycle.Object, NullLogger<BudgetForecastService>.Instance);

        _repo.Setup(r => r.GetAllDepartmentsAsync()).ReturnsAsync(_departments);
        _repo.Setup(r => r.GetDepartmentAsync(DeptA)).ReturnsAsync((DeptA, "IT-A"));
        _repo.Setup(r => r.GetDepartmentAsync(DeptB)).ReturnsAsync((DeptB, "IT-B"));
        _repo.Setup(r => r.GetByYearAndDepartmentAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((BudgetForecast?)null);
        _repo.Setup(r => r.GetAllPricesAsync()).ReturnsAsync(new List<AssetCategoryReferencePrice>
        {
            new() { CategoryId = CatComputer, UnitPrice = 20_000_000m },
            new() { CategoryId = CatSwitch, UnitPrice = 5_000_000m },
        });
        _repo.Setup(r => r.GetAllCategoriesAsync()).ReturnsAsync(new List<(int Id, string Name)>
        {
            (CatComputer, "Máy tính"), (CatPrinter, "Máy in"), (CatSwitch, "Switch"),
        });
        SetupCandidates();
    }

    private void SetupCandidates(params AssetReplacementCandidateDto[] candidates) =>
        _lifecycle.Setup(l => l.GetReplacementCandidatesAsync(It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(candidates.ToList());

    private static AssetReplacementCandidateDto Candidate(
        int categoryId, string categoryName, int dueYear, int departmentId = DeptA, string? code = null) => new()
    {
        AssetId = 1,
        AssetCode = code ?? $"A-{Guid.NewGuid():N}"[..8],
        AssetName = "x",
        CategoryId = categoryId,
        CategoryName = categoryName,
        DepartmentId = departmentId,
        DepartmentName = departmentId == DeptA ? "IT-A" : "IT-B",
        DueYear = dueYear,
    };

    // ---------- Validate đầu vào ----------

    [Fact]
    public async Task GenerateAsync_YearInPast_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year - 1 }));
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_YearMoreThanTenYearsAhead_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year + 11 }));
    }

    [Fact]
    public async Task GenerateAsync_YearExactlyTenYearsAhead_IsAccepted()
    {
        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year + 10, DepartmentId = DeptA });
        Assert.Single(result.Forecasts);
    }

    [Fact]
    public async Task GenerateAsync_DepartmentNotFound_ThrowsArgumentException()
    {
        _repo.Setup(r => r.GetDepartmentAsync(99)).ReturnsAsync(((int, string)?)null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = 99 }));
    }

    // ---------- Hợp đồng với module Vòng đời ----------

    [Fact]
    public async Task GenerateAsync_PassesForecastYearAsHorizonYearToLifecycleService()
    {
        await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year + 2, DepartmentId = DeptA });

        _lifecycle.Verify(l => l.GetReplacementCandidatesAsync(DeptA, Year + 2), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_DepartmentIdNull_PassesNullDepartmentToLifecycleService()
    {
        await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year });

        _lifecycle.Verify(l => l.GetReplacementCandidatesAsync(null, Year), Times.Once);
    }

    // ---------- D6: không cộng dồn các năm ----------

    [Fact]
    public async Task GenerateAsync_OnlyCountsCandidatesWhoseDueYearEqualsForecastYear()
    {
        var forecastYear = Year + 1;
        SetupCandidates(
            Candidate(CatComputer, "Máy tính", Year),             // đã tới hạn từ năm trước đó → KHÔNG tính vào Y+1
            Candidate(CatComputer, "Máy tính", forecastYear),
            Candidate(CatComputer, "Máy tính", forecastYear),
            Candidate(CatSwitch, "Switch", forecastYear),
            Candidate(CatSwitch, "Switch", forecastYear + 1));    // tới hạn năm sau nữa → KHÔNG tính

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = forecastYear, DepartmentId = DeptA });

        var forecast = Assert.Single(result.Forecasts);
        Assert.Equal(3, forecast.EstimatedReplacementCount);
        Assert.Equal(2 * 20_000_000m + 5_000_000m, forecast.EstimatedBudget);
    }

    // ---------- D7: ngân sách & thiếu đơn giá ----------

    [Fact]
    public async Task GenerateAsync_AllCategoriesPriced_BudgetEqualsSumOfCountTimesUnitPrice()
    {
        SetupCandidates(
            Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year),
            Candidate(CatSwitch, "Switch", Year));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        var forecast = Assert.Single(result.Forecasts);
        Assert.Equal(4, forecast.EstimatedReplacementCount);
        Assert.Equal(65_000_000m, forecast.EstimatedBudget);
        Assert.Null(forecast.Notes);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, forecast.Breakdown.Count);
        var computers = forecast.Breakdown.Single(b => b.CategoryId == CatComputer);
        Assert.Equal(3, computers.AssetCount);
        Assert.Equal(20_000_000m, computers.UnitPrice);
        Assert.Equal(60_000_000m, computers.Subtotal);
    }

    [Fact]
    public async Task GenerateAsync_CategoryWithoutPrice_StillCountedButExcludedFromBudgetAndReportedInWarnings()
    {
        // Kịch bản C.2 (IT-A, năm Y): 3 Máy tính (có giá) + 1 Máy in (không có giá).
        SetupCandidates(
            Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year),
            Candidate(CatPrinter, "Máy in", Year));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        var forecast = Assert.Single(result.Forecasts);
        Assert.Equal(4, forecast.EstimatedReplacementCount);          // vẫn ĐẾM Máy in
        Assert.Equal(60_000_000m, forecast.EstimatedBudget);          // nhưng KHÔNG cộng vào ngân sách

        var printer = forecast.Breakdown.Single(b => b.CategoryId == CatPrinter);
        Assert.Equal(1, printer.AssetCount);
        Assert.Null(printer.UnitPrice);
        Assert.Null(printer.Subtotal);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Máy in", warning);
        Assert.Contains("1", warning);
        Assert.Contains("Máy in", forecast.Notes);
    }

    [Fact]
    public async Task GenerateAsync_NoCandidates_CreatesRowWithZeroCountAndZeroBudget()
    {
        BudgetForecast? added = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<BudgetForecast>())).Callback<BudgetForecast>(f => added = f).Returns(Task.CompletedTask);

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        var forecast = Assert.Single(result.Forecasts);
        Assert.Equal(0, forecast.EstimatedReplacementCount);
        Assert.Equal(0m, forecast.EstimatedBudget);
        Assert.Empty(forecast.Breakdown);
        Assert.NotNull(added);
        Assert.NotNull(added!.GeneratedAt);
    }

    // ---------- D8: upsert ----------

    [Fact]
    public async Task GenerateAsync_ExistingRowForSameYearAndDepartment_UpdatesInsteadOfAdding()
    {
        var existing = new BudgetForecast
        {
            Id = 7, Year = Year, DepartmentId = DeptA, EstimatedReplacementCount = 99, EstimatedBudget = 1m, Notes = "cũ",
        };
        _repo.Setup(r => r.GetByYearAndDepartmentAsync(Year, DeptA)).ReturnsAsync(existing);
        SetupCandidates(Candidate(CatComputer, "Máy tính", Year));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        _repo.Verify(r => r.AddAsync(It.IsAny<BudgetForecast>()), Times.Never);
        _repo.Verify(r => r.Update(existing), Times.Once);
        Assert.Equal(1, existing.EstimatedReplacementCount);
        Assert.Equal(20_000_000m, existing.EstimatedBudget);
        Assert.Null(existing.Notes);                                  // ghi đè cả ghi chú cũ
        Assert.Equal(7, Assert.Single(result.Forecasts).Id);
    }

    [Fact]
    public async Task GenerateAsync_SoftDeletedRowForSameYearAndDepartment_IsRestoredNotDuplicated()
    {
        var deleted = new BudgetForecast { Id = 8, Year = Year, DepartmentId = DeptA, IsDeleted = true, DeletedAt = DateTime.UtcNow };
        _repo.Setup(r => r.GetByYearAndDepartmentAsync(Year, DeptA)).ReturnsAsync(deleted);
        SetupCandidates(Candidate(CatComputer, "Máy tính", Year));

        await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        Assert.False(deleted.IsDeleted);
        Assert.Null(deleted.DeletedAt);
        _repo.Verify(r => r.AddAsync(It.IsAny<BudgetForecast>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_BreakdownListsTheAssetsBehindEachCount()
    {
        SetupCandidates(Candidate(CatComputer, "Máy tính", Year, code: "PC-001"), Candidate(CatComputer, "Máy tính", Year, code: "PC-002"));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        var item = Assert.Single(Assert.Single(result.Forecasts).Breakdown);
        Assert.Equal(new[] { "PC-001", "PC-002" }, item.Assets.Select(a => a.AssetCode));
    }

    [Fact]
    public async Task GenerateAsync_DepartmentIdNull_CreatesOneRowPerDepartment()
    {
        SetupCandidates(
            Candidate(CatComputer, "Máy tính", Year, DeptA),
            Candidate(CatComputer, "Máy tính", Year, DeptB));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year });

        Assert.Equal(2, result.Forecasts.Count);
        _repo.Verify(r => r.AddAsync(It.IsAny<BudgetForecast>()), Times.Exactly(2));
        Assert.All(result.Forecasts, f => Assert.Equal(1, f.EstimatedReplacementCount));
        Assert.Equal(new[] { "IT-A", "IT-B" }, result.Forecasts.Select(f => f.DepartmentName));
    }

    [Fact]
    public async Task GenerateAsync_DepartmentIdNull_AggregatesMissingPriceWarningAcrossDepartments()
    {
        SetupCandidates(
            Candidate(CatPrinter, "Máy in", Year, DeptA),
            Candidate(CatPrinter, "Máy in", Year, DeptB),
            Candidate(CatPrinter, "Máy in", Year, DeptB));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year });

        var warning = Assert.Single(result.Warnings);                 // 1 cảnh báo cho cả lần chạy, không lặp theo phòng ban
        Assert.Contains("3", warning);
    }

    [Fact]
    public async Task GenerateAsync_SavesChangesExactlyOnceForAllRows()
    {
        await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year });

        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_LongMissingPriceList_NotesTruncatedToColumnLimit()
    {
        var candidates = Enumerable.Range(1, 60)
            .Select(i => Candidate(1000 + i, $"Loại thiết bị có tên khá dài số {i}", Year))
            .ToArray();
        SetupCandidates(candidates);

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        var notes = Assert.Single(result.Forecasts).Notes;
        Assert.NotNull(notes);
        Assert.True(notes!.Length <= BudgetForecastService.NotesMaxLength);
    }

    [Fact]
    public async Task GenerateAsync_BudgetUsesDecimalWithoutFloatingPointDrift()
    {
        _repo.Setup(r => r.GetAllPricesAsync()).ReturnsAsync(new List<AssetCategoryReferencePrice>
        {
            new() { CategoryId = CatComputer, UnitPrice = 0.10m },
        });
        SetupCandidates(
            Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year), Candidate(CatComputer, "Máy tính", Year));

        var result = await _sut.GenerateAsync(new GenerateForecastRequestDto { Year = Year, DepartmentId = DeptA });

        Assert.Equal(0.30m, Assert.Single(result.Forecasts).EstimatedBudget);   // double sẽ ra 0.30000000000000004
    }

    // ---------- Đọc dự báo ----------

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsBudgetForecastNotFoundException()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync((BudgetForecast?)null);

        await Assert.ThrowsAsync<BudgetForecastNotFoundException>(() => _sut.GetByIdAsync(5));
    }

    [Fact]
    public async Task GetByIdAsync_ValidBreakdownJson_DeserializesBreakdown()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(new BudgetForecast
        {
            Id = 5, Year = Year, DepartmentId = DeptA, Department = new Department { Id = DeptA, Name = "IT-A" },
            BreakdownJson = """[{"categoryId":10,"categoryName":"Máy tính","assetCount":3,"unitPrice":20000000,"subtotal":60000000}]""",
        });

        var result = await _sut.GetByIdAsync(5);

        var item = Assert.Single(result.Breakdown);
        Assert.Equal("Máy tính", item.CategoryName);
        Assert.Equal(3, item.AssetCount);
        Assert.Equal(60_000_000m, item.Subtotal);
        Assert.Equal("IT-A", result.DepartmentName);
    }

    [Fact]
    public async Task GetByIdAsync_CorruptBreakdownJson_ReturnsEmptyBreakdownWithoutThrowing()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(new BudgetForecast
        {
            Id = 5, Year = Year, DepartmentId = DeptA, Department = new Department { Id = DeptA, Name = "IT-A" },
            BreakdownJson = "{không phải json",
        });

        var result = await _sut.GetByIdAsync(5);

        Assert.Empty(result.Breakdown);
    }

    [Fact]
    public async Task GetByIdAsync_NullBreakdownJson_ReturnsEmptyBreakdown()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(new BudgetForecast
        {
            Id = 5, Year = Year, DepartmentId = DeptA, Department = new Department { Id = DeptA, Name = "IT-A" },
        });

        Assert.Empty((await _sut.GetByIdAsync(5)).Breakdown);
    }

    [Fact]
    public async Task GetPagedAsync_OutOfRangePagingFallsBackToDefaults()
    {
        _repo.Setup(r => r.GetPagedAsync(null, null, 1, 20)).ReturnsAsync((new List<BudgetForecast>(), 0));

        var result = await _sut.GetPagedAsync(null, null, 0, 5000);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    // ---------- Đơn giá tham khảo ----------

    [Fact]
    public async Task UpsertPriceAsync_CategoryNotFound_ThrowsAssetCategoryNotFoundException()
    {
        await Assert.ThrowsAsync<AssetCategoryNotFoundException>(() =>
            _sut.UpsertPriceAsync(999, new UpsertReferencePriceRequestDto { UnitPrice = 1_000m }));
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task UpsertPriceAsync_NonPositivePrice_ThrowsArgumentException(int price)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpsertPriceAsync(CatComputer, new UpsertReferencePriceRequestDto { UnitPrice = price }));
    }

    [Fact]
    public async Task UpsertPriceAsync_NewPrice_AddsAndSaves()
    {
        _repo.Setup(r => r.GetPriceAsync(CatPrinter)).ReturnsAsync((AssetCategoryReferencePrice?)null);

        var result = await _sut.UpsertPriceAsync(CatPrinter, new UpsertReferencePriceRequestDto { UnitPrice = 3_500_000m });

        _repo.Verify(r => r.AddPriceAsync(It.Is<AssetCategoryReferencePrice>(p => p.CategoryId == CatPrinter && p.UnitPrice == 3_500_000m)), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        Assert.Equal("Máy in", result.CategoryName);
        Assert.Equal(3_500_000m, result.UnitPrice);
    }

    [Fact]
    public async Task UpsertPriceAsync_ExistingPrice_UpdatesInsteadOfAdding()
    {
        var existing = new AssetCategoryReferencePrice { CategoryId = CatComputer, UnitPrice = 20_000_000m };
        _repo.Setup(r => r.GetPriceAsync(CatComputer)).ReturnsAsync(existing);

        var result = await _sut.UpsertPriceAsync(CatComputer, new UpsertReferencePriceRequestDto { UnitPrice = 22_000_000m });

        _repo.Verify(r => r.AddPriceAsync(It.IsAny<AssetCategoryReferencePrice>()), Times.Never);
        _repo.Verify(r => r.UpdatePrice(existing), Times.Once);
        Assert.Equal(22_000_000m, existing.UnitPrice);
        Assert.Equal(22_000_000m, result.UnitPrice);
    }

    [Fact]
    public async Task DeletePriceAsync_NoPriceConfigured_ThrowsReferencePriceNotFoundException()
    {
        _repo.Setup(r => r.GetPriceAsync(CatPrinter)).ReturnsAsync((AssetCategoryReferencePrice?)null);

        await Assert.ThrowsAsync<ReferencePriceNotFoundException>(() => _sut.DeletePriceAsync(CatPrinter));
    }

    [Fact]
    public async Task DeletePriceAsync_ExistingPrice_RemovesAndSaves()
    {
        var existing = new AssetCategoryReferencePrice { CategoryId = CatComputer, UnitPrice = 1m };
        _repo.Setup(r => r.GetPriceAsync(CatComputer)).ReturnsAsync(existing);

        await _sut.DeletePriceAsync(CatComputer);

        // Xoá mềm: chỉ đặt cờ IsDeleted, không xoá dòng khỏi DB.
        Assert.True(existing.IsDeleted);
        Assert.NotNull(existing.DeletedAt);
        _repo.Verify(r => r.UpdatePrice(existing), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeletePriceAsync_AlreadySoftDeleted_ThrowsReferencePriceNotFoundException()
    {
        var deleted = new AssetCategoryReferencePrice { CategoryId = CatComputer, UnitPrice = 1m, IsDeleted = true };
        _repo.Setup(r => r.GetPriceAsync(CatComputer)).ReturnsAsync(deleted);

        await Assert.ThrowsAsync<ReferencePriceNotFoundException>(() => _sut.DeletePriceAsync(CatComputer));
    }

    [Fact]
    public async Task UpsertPriceAsync_SoftDeletedPrice_IsRestoredNotDuplicated()
    {
        var deleted = new AssetCategoryReferencePrice { CategoryId = CatComputer, UnitPrice = 1m, IsDeleted = true, DeletedAt = DateTime.UtcNow };
        _repo.Setup(r => r.GetPriceAsync(CatComputer)).ReturnsAsync(deleted);

        await _sut.UpsertPriceAsync(CatComputer, new UpsertReferencePriceRequestDto { UnitPrice = 9_000_000m });

        Assert.False(deleted.IsDeleted);
        Assert.Null(deleted.DeletedAt);
        Assert.Equal(9_000_000m, deleted.UnitPrice);
        _repo.Verify(r => r.AddPriceAsync(It.IsAny<AssetCategoryReferencePrice>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsBudgetForecastNotFoundException()
    {
        _repo.Setup(r => r.GetTrackedByIdAsync(99)).ReturnsAsync((BudgetForecast?)null);

        await Assert.ThrowsAsync<BudgetForecastNotFoundException>(() => _sut.DeleteAsync(99));
    }

    [Fact]
    public async Task DeleteAsync_Existing_SoftDeletesOnly()
    {
        var forecast = new BudgetForecast { Id = 5, Year = 2027, DepartmentId = 1 };
        _repo.Setup(r => r.GetTrackedByIdAsync(5)).ReturnsAsync(forecast);

        await _sut.DeleteAsync(5);

        Assert.True(forecast.IsDeleted);
        Assert.NotNull(forecast.DeletedAt);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetPricesAsync_CategoryWithoutPrice_ReturnsNullUnitPrice()
    {
        var result = await _sut.GetPricesAsync();

        Assert.Equal(3, result.Count);                                // liệt kê MỌI loại
        Assert.Equal(20_000_000m, result.Single(p => p.CategoryId == CatComputer).UnitPrice);
        Assert.Null(result.Single(p => p.CategoryId == CatPrinter).UnitPrice);
    }

    // ---------- Validator ----------

    [Fact]
    public void GenerateForecastValidator_YearOutOfRange_IsInvalid()
    {
        var validator = new GenerateForecastRequestValidator();

        Assert.False(validator.Validate(new GenerateForecastRequestDto { Year = Year - 1 }).IsValid);
        Assert.False(validator.Validate(new GenerateForecastRequestDto { Year = Year + 11 }).IsValid);
        Assert.True(validator.Validate(new GenerateForecastRequestDto { Year = Year }).IsValid);
        Assert.True(validator.Validate(new GenerateForecastRequestDto { Year = Year + 10 }).IsValid);
        Assert.False(validator.Validate(new GenerateForecastRequestDto { Year = Year, DepartmentId = 0 }).IsValid);
    }
}
