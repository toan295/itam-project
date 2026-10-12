using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Validators;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITAM.Tests.Services;

public class ExpansionPlanningServiceTests
{
    private const int DeptA = 1;
    private const int DeptB = 2;
    private const int CatComputer = 10;
    private const int CatPrinter = 11;

    private static readonly int Year = DateTime.UtcNow.Year;

    private readonly Mock<IBudgetForecastRepository> _repo = new();
    private readonly ExpansionPlanningService _sut;

    public ExpansionPlanningServiceTests()
    {
        _sut = new ExpansionPlanningService(_repo.Object);

        _repo.Setup(r => r.GetAllDepartmentsAsync())
            .ReturnsAsync(new List<(int Id, string Name)> { (DeptA, "IT-A"), (DeptB, "IT-B") });
        _repo.Setup(r => r.GetDepartmentAsync(DeptA)).ReturnsAsync((DeptA, "IT-A"));
        _repo.Setup(r => r.GetDepartmentAsync(DeptB)).ReturnsAsync((DeptB, "IT-B"));
        _repo.Setup(r => r.GetAllCategoriesAsync())
            .ReturnsAsync(new List<(int Id, string Name)> { (CatComputer, "Máy tính"), (CatPrinter, "Máy in") });
        _repo.Setup(r => r.GetPlansAsync(It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(new List<DepartmentExpansionPlan>());
        _repo.Setup(r => r.GetAllRatiosAsync()).ReturnsAsync(new List<AssetCategoryDeviceRatio>());
    }

    // ---- Kế hoạch ----

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task GetPlansAsync_YearOutOfRange_ThrowsArgumentException(int offsetFromCurrent)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPlansAsync(Year + offsetFromCurrent, null));
    }

    [Fact]
    public async Task GetPlansAsync_DepartmentWithoutPlan_ReturnsHasPlanFalseWithZeroHeadcount()
    {
        _repo.Setup(r => r.GetPlansAsync(Year, null)).ReturnsAsync(new List<DepartmentExpansionPlan>
        {
            new() { Year = Year, DepartmentId = DeptA, AdditionalHeadcount = 5, UpdatedAt = DateTime.UtcNow },
        });

        var result = await _sut.GetPlansAsync(Year, null);

        Assert.Equal(2, result.Count);   // liệt kê MỌI phòng ban
        var a = result.Single(p => p.DepartmentId == DeptA);
        Assert.True(a.HasPlan);
        Assert.Equal(5, a.AdditionalHeadcount);
        var b = result.Single(p => p.DepartmentId == DeptB);
        Assert.False(b.HasPlan);
        Assert.Equal(0, b.AdditionalHeadcount);
        Assert.Null(b.UpdatedAt);
    }

    [Fact]
    public async Task GetPlansAsync_UnknownDepartmentFilter_ThrowsDepartmentNotFound()
    {
        _repo.Setup(r => r.GetDepartmentAsync(99)).ReturnsAsync(((int, string)?)null);

        await Assert.ThrowsAsync<DepartmentNotFoundException>(() => _sut.GetPlansAsync(Year, 99));
    }

    [Fact]
    public async Task UpsertPlanAsync_DepartmentNotFound_ThrowsDepartmentNotFoundException()
    {
        _repo.Setup(r => r.GetDepartmentAsync(99)).ReturnsAsync(((int, string)?)null);

        await Assert.ThrowsAsync<DepartmentNotFoundException>(() =>
            _sut.UpsertPlanAsync(Year, 99, new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 3 }));
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpsertPlanAsync_NewPlan_AddsAndReturnsDto()
    {
        _repo.Setup(r => r.GetPlanAsync(Year, DeptA)).ReturnsAsync((DepartmentExpansionPlan?)null);
        DepartmentExpansionPlan? added = null;
        _repo.Setup(r => r.AddPlanAsync(It.IsAny<DepartmentExpansionPlan>()))
            .Callback<DepartmentExpansionPlan>(p => added = p).Returns(Task.CompletedTask);

        var result = await _sut.UpsertPlanAsync(Year, DeptA, new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 8, Notes = " tuyển thêm " });

        Assert.NotNull(added);
        Assert.Equal((Year, DeptA, 8), (added!.Year, added.DepartmentId, added.AdditionalHeadcount));
        Assert.Equal("tuyển thêm", added.Notes);
        Assert.True(result.HasPlan);
        Assert.Equal("IT-A", result.DepartmentName);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpsertPlanAsync_ExistingPlan_UpdatesAndRefreshesUpdatedAt()
    {
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new DepartmentExpansionPlan { Id = 5, Year = Year, DepartmentId = DeptA, AdditionalHeadcount = 2, UpdatedAt = old };
        _repo.Setup(r => r.GetPlanAsync(Year, DeptA)).ReturnsAsync(existing);

        var result = await _sut.UpsertPlanAsync(Year, DeptA, new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 9 });

        Assert.Equal(9, existing.AdditionalHeadcount);
        Assert.True(existing.UpdatedAt > old);
        Assert.Equal(9, result.AdditionalHeadcount);
        _repo.Verify(r => r.AddPlanAsync(It.IsAny<DepartmentExpansionPlan>()), Times.Never);
    }

    [Fact]
    public async Task UpsertPlanAsync_UniqueIndexViolation_ThrowsExpansionPlanConflictException()
    {
        _repo.Setup(r => r.GetPlanAsync(Year, DeptA)).ReturnsAsync((DepartmentExpansionPlan?)null);
        _repo.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException("duplicate"));

        await Assert.ThrowsAsync<ExpansionPlanConflictException>(() =>
            _sut.UpsertPlanAsync(Year, DeptA, new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 1 }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5001)]
    public async Task UpsertPlanAsync_HeadcountOutOfRange_ThrowsArgumentException(int headcount)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpsertPlanAsync(Year, DeptA, new UpsertExpansionPlanRequestDto { AdditionalHeadcount = headcount }));
    }

    [Fact]
    public async Task DeletePlanAsync_NoPlan_ThrowsExpansionPlanNotFoundException()
    {
        _repo.Setup(r => r.GetPlanAsync(Year, DeptA)).ReturnsAsync((DepartmentExpansionPlan?)null);

        await Assert.ThrowsAsync<ExpansionPlanNotFoundException>(() => _sut.DeletePlanAsync(Year, DeptA));
    }

    [Fact]
    public async Task DeletePlanAsync_ExistingPlan_RemovesAndSaves()
    {
        var plan = new DepartmentExpansionPlan { Year = Year, DepartmentId = DeptA };
        _repo.Setup(r => r.GetPlanAsync(Year, DeptA)).ReturnsAsync(plan);

        await _sut.DeletePlanAsync(Year, DeptA);

        _repo.Verify(r => r.RemovePlan(plan), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ---- Định mức ----

    [Fact]
    public async Task GetRatiosAsync_CategoryWithoutRatio_ReturnsNullDevicesPerPerson()
    {
        _repo.Setup(r => r.GetAllRatiosAsync()).ReturnsAsync(new List<AssetCategoryDeviceRatio>
        {
            new() { CategoryId = CatComputer, DevicesPerPerson = 1.00m },
        });

        var result = await _sut.GetRatiosAsync();

        Assert.Equal(2, result.Count);   // MỌI loại
        Assert.Equal(1.00m, result.Single(r => r.CategoryId == CatComputer).DevicesPerPerson);
        Assert.Null(result.Single(r => r.CategoryId == CatPrinter).DevicesPerPerson);
    }

    [Fact]
    public async Task UpsertRatioAsync_CategoryNotFound_ThrowsAssetCategoryNotFoundException()
    {
        await Assert.ThrowsAsync<AssetCategoryNotFoundException>(() =>
            _sut.UpsertRatioAsync(999, new UpsertDeviceRatioRequestDto { DevicesPerPerson = 1m }));
    }

    [Fact]
    public async Task UpsertRatioAsync_NewRatio_Adds()
    {
        _repo.Setup(r => r.GetRatioAsync(CatComputer)).ReturnsAsync((AssetCategoryDeviceRatio?)null);

        var result = await _sut.UpsertRatioAsync(CatComputer, new UpsertDeviceRatioRequestDto { DevicesPerPerson = 1.5m });

        _repo.Verify(r => r.AddRatioAsync(It.Is<AssetCategoryDeviceRatio>(x => x.CategoryId == CatComputer && x.DevicesPerPerson == 1.5m)), Times.Once);
        Assert.Equal(1.5m, result.DevicesPerPerson);
        Assert.Equal("Máy tính", result.CategoryName);
    }

    [Fact]
    public async Task UpsertRatioAsync_ExistingRatio_UpdatesInsteadOfAdding()
    {
        var existing = new AssetCategoryDeviceRatio { CategoryId = CatComputer, DevicesPerPerson = 1m };
        _repo.Setup(r => r.GetRatioAsync(CatComputer)).ReturnsAsync(existing);

        await _sut.UpsertRatioAsync(CatComputer, new UpsertDeviceRatioRequestDto { DevicesPerPerson = 2m });

        Assert.Equal(2m, existing.DevicesPerPerson);
        _repo.Verify(r => r.AddRatioAsync(It.IsAny<AssetCategoryDeviceRatio>()), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20.01)]
    [InlineData(1.005)]
    public async Task UpsertRatioAsync_InvalidValue_ThrowsArgumentException(double value)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpsertRatioAsync(CatComputer, new UpsertDeviceRatioRequestDto { DevicesPerPerson = (decimal)value }));
    }

    [Fact]
    public async Task DeleteRatioAsync_NoRatio_ThrowsDeviceRatioNotFoundException()
    {
        _repo.Setup(r => r.GetRatioAsync(CatComputer)).ReturnsAsync((AssetCategoryDeviceRatio?)null);

        await Assert.ThrowsAsync<DeviceRatioNotFoundException>(() => _sut.DeleteRatioAsync(CatComputer));
    }

    // ---- Validator ----

    [Theory]
    [InlineData(0.001)]
    [InlineData(1.005)]
    [InlineData(1.234)]
    public void UpsertDeviceRatio_MoreThanTwoDecimals_Fails(double value)
    {
        var result = new UpsertDeviceRatioRequestValidator().Validate(new UpsertDeviceRatioRequestDto { DevicesPerPerson = (decimal)value });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20.01)]
    public void UpsertDeviceRatio_OutOfRange_Fails(double value)
    {
        var result = new UpsertDeviceRatioRequestValidator().Validate(new UpsertDeviceRatioRequestDto { DevicesPerPerson = (decimal)value });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.10)]
    [InlineData(20)]
    public void UpsertDeviceRatio_BoundaryValues_Pass(double value)
    {
        var result = new UpsertDeviceRatioRequestValidator().Validate(new UpsertDeviceRatioRequestDto { DevicesPerPerson = (decimal)value });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpsertExpansionPlan_NegativeHeadcount_Fails()
    {
        var result = new UpsertExpansionPlanRequestValidator().Validate(new UpsertExpansionPlanRequestDto { AdditionalHeadcount = -1 });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpsertExpansionPlan_HeadcountOver5000_Fails()
    {
        var result = new UpsertExpansionPlanRequestValidator().Validate(new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 5001 });
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    public void UpsertExpansionPlan_BoundaryHeadcount_Passes(int headcount)
    {
        var result = new UpsertExpansionPlanRequestValidator().Validate(new UpsertExpansionPlanRequestDto { AdditionalHeadcount = headcount });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpsertExpansionPlan_NotesOver500_Fails()
    {
        var result = new UpsertExpansionPlanRequestValidator().Validate(
            new UpsertExpansionPlanRequestDto { AdditionalHeadcount = 1, Notes = new string('x', 501) });
        Assert.False(result.IsValid);
    }
}
