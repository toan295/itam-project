using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Services.Implementations;

namespace ITAM.Tests.Services;

public class LifecycleCalculatorTests
{
    [Fact]
    public void Evaluate_ExactlyFiveYears_IsOverdueByAge()
    {
        var row = CreateRow(new DateOnly(2021, 10, 5), failedTicketCount: 0);
        var today = new DateOnly(2026, 10, 5);

        var result = LifecycleCalculator.Evaluate(row, maxAgeYears: 5, maxFailureCount: 3, today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(2026, result.DueYear);
        Assert.Contains("Quá tuổi", result.Reasons);
    }

    [Fact]
    public void Evaluate_AgeDueLaterThisYear_IsNotOverdueNow()
    {
        var row = CreateRow(new DateOnly(2021, 12, 1), failedTicketCount: 0);
        var today = new DateOnly(2026, 10, 5);

        var result = LifecycleCalculator.Evaluate(row, maxAgeYears: 5, maxFailureCount: 3, today);

        Assert.False(result.IsOverdueNow);
        Assert.Equal(2026, result.DueYear);
    }

    [Fact]
    public void Evaluate_FailureThresholdReached_IsOverdueNow()
    {
        var row = CreateRow(purchaseDate: null, failedTicketCount: 3);
        var today = new DateOnly(2026, 10, 5);

        var result = LifecycleCalculator.Evaluate(row, maxAgeYears: 5, maxFailureCount: 3, today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(2026, result.DueYear);
        Assert.Contains("Lỗi nhiều", result.Reasons);
    }

    [Fact]
    public void Evaluate_AgeAndFailureReached_HasHighPriority()
    {
        var row = CreateRow(new DateOnly(2020, 1, 1), failedTicketCount: 4);
        var today = new DateOnly(2026, 10, 5);

        var result = LifecycleCalculator.Evaluate(row, maxAgeYears: 5, maxFailureCount: 3, today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(2, result.Priority);
        Assert.Contains("Quá tuổi", result.Reasons);
        Assert.Contains("Lỗi nhiều", result.Reasons);
    }

    [Fact]
    public void ComputeAgeYears_UnknownPurchaseDate_ReturnsNull()
    {
        var result = LifecycleCalculator.ComputeAgeYears(null, new DateOnly(2026, 10, 5));

        Assert.Null(result);
    }

    private static AssetLifecycleRow CreateRow(DateOnly? purchaseDate, int failedTicketCount) => new()
    {
        AssetId = 1,
        AssetCode = "PC-001",
        AssetName = "Test Asset",
        CategoryId = 1,
        CategoryName = "Laptop",
        DepartmentId = 1,
        DepartmentName = "IT",
        PurchaseDate = purchaseDate,
        TicketCount = failedTicketCount,
        FailedTicketCount = failedTicketCount,
    };
}
