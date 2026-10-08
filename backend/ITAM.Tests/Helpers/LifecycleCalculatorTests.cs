using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.Tests.Helpers;

public class LifecycleCalculatorTests
{
    // Ngày cố định để kết quả không phụ thuộc ngày chạy test.
    private static readonly DateOnly Today = new(2026, 10, 5);

    // ---- ComputeAgeYears (D1) ----

    [Fact]
    public void ComputeAgeYears_UnknownPurchaseDate_ReturnsNull()
    {
        Assert.Null(LifecycleCalculator.ComputeAgeYears(null, Today));
    }

    [Fact]
    public void ComputeAgeYears_PurchaseDateInFuture_ReturnsZero()
    {
        Assert.Equal(0d, LifecycleCalculator.ComputeAgeYears(Today.AddDays(30), Today));
    }

    [Fact]
    public void ComputeAgeYears_TwoYearsAgo_ReturnsApproximatelyTwo()
    {
        var age = LifecycleCalculator.ComputeAgeYears(Today.AddYears(-2), Today);

        Assert.InRange(age!.Value, 1.99, 2.01);
    }

    // ---- Ngưỡng tuổi (D3: bao gồm dấu bằng, so theo ngày lịch) ----

    [Fact]
    public void Evaluate_AgeExactlyAtMaxYears_IsAgeExceeded()
    {
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-5), 0), 5, 3, Today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(new[] { LifecycleCalculator.ReasonAgeExceeded }, result.Reasons);
        Assert.Equal(Today.Year, result.DueYear);
    }

    [Fact]
    public void Evaluate_OneDayBeforeMaxAge_IsNotAgeExceeded()
    {
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-5).AddDays(1), 0), 5, 3, Today);

        Assert.False(result.IsOverdueNow);
        Assert.Empty(result.Reasons);
    }

    // ---- Ngưỡng số lần lỗi (D2: mọi phiếu, mọi trạng thái; D3: bao gồm dấu bằng) ----

    [Fact]
    public void Evaluate_TicketCountEqualsThreshold_IsFailureExceeded()
    {
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 3), 5, 3, Today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(new[] { LifecycleCalculator.ReasonFailureCountExceeded }, result.Reasons);
    }

    [Fact]
    public void Evaluate_TicketCountBelowThreshold_IsNotFailureExceeded()
    {
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 2), 5, 3, Today);

        Assert.False(result.IsOverdueNow);
    }

    [Fact]
    public void Evaluate_OnlyFailedTicketsAreNotRequired_CountsAllTickets()
    {
        // 3 phiếu nhưng không phiếu nào ở trạng thái Failed: vẫn tính là vượt ngưỡng (D2).
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 3, failedTicketCount: 0), 5, 3, Today);

        Assert.True(result.IsOverdueNow);
    }

    [Fact]
    public void Evaluate_ManyFailedTicketsButFewTotal_UsesTotalNotFailed()
    {
        // Dữ liệu không thể xảy ra trong DB (Failed <= Total) nhưng khoá đúng nguồn so sánh là TicketCount.
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 1, failedTicketCount: 5), 5, 3, Today);

        Assert.False(result.IsOverdueNow);
    }

    // ---- DueYear (D5) ----

    [Fact]
    public void Evaluate_NoPurchaseDateAndNoFailure_DueYearIsNull()
    {
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 1), 5, 3, Today);

        Assert.Null(result.DueYear);
        Assert.False(result.IsOverdueNow);
    }

    [Fact]
    public void Evaluate_NoPurchaseDateButFailureExceeded_DueYearIsCurrentYear()
    {
        var result = LifecycleCalculator.Evaluate(Row(null, ticketCount: 4), 5, 3, Today);

        Assert.Equal(Today.Year, result.DueYear);
    }

    [Fact]
    public void Evaluate_AgeNotYetReached_DueYearIsYearOfAgeDueDate()
    {
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-2), 0), 5, 3, Today);

        Assert.Equal(Today.Year + 3, result.DueYear);
        Assert.False(result.IsOverdueNow);
        Assert.Empty(result.Reasons);   // chưa vượt ngưỡng thì chưa có lý do nào
    }

    [Fact]
    public void Evaluate_AgeDueLaterThisYear_IsNotOverdueNowButDueThisYear()
    {
        var result = LifecycleCalculator.Evaluate(Row(new DateOnly(2021, 12, 1), 0), 5, 3, Today);

        Assert.False(result.IsOverdueNow);
        Assert.Equal(2026, result.DueYear);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(30)]
    public void Evaluate_AgeExceededLongAgo_DueYearIsCurrentYearNotThePast(int ageYears)
    {
        // Hồi quy: DueYear từng bị tính theo năm của ngày tới hạn (quá khứ) → dự báo năm hiện tại bỏ sót tài sản.
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-ageYears), 0), 5, 3, Today);

        Assert.True(result.IsOverdueNow);
        Assert.Equal(Today.Year, result.DueYear);
    }

    [Fact]
    public void Evaluate_AgeLongExceededAndFailureExceeded_DueYearIsCurrentYear()
    {
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-9), 4), 5, 3, Today);

        Assert.Equal(Today.Year, result.DueYear);
    }

    // ---- Ưu tiên cao ----

    [Fact]
    public void Evaluate_BothReasons_IsHighPriority()
    {
        var result = LifecycleCalculator.Evaluate(Row(Today.AddYears(-6), 4), 5, 3, Today);

        Assert.True(result.IsHighPriority);
        Assert.Equal(
            new[] { LifecycleCalculator.ReasonAgeExceeded, LifecycleCalculator.ReasonFailureCountExceeded },
            result.Reasons);
    }

    [Fact]
    public void Evaluate_OnlyOneReason_IsNotHighPriority()
    {
        Assert.False(LifecycleCalculator.Evaluate(Row(Today.AddYears(-6), 0), 5, 3, Today).IsHighPriority);
        Assert.False(LifecycleCalculator.Evaluate(Row(null, 4), 5, 3, Today).IsHighPriority);
    }

    // ---- Ngưỡng không hợp lệ ----

    [Theory]
    [InlineData(0, 3)]
    [InlineData(5, 0)]
    public void Evaluate_NonPositiveThreshold_ThrowsArgumentException(int maxAge, int maxFailure)
    {
        Assert.Throws<ArgumentException>(() =>
            LifecycleCalculator.Evaluate(Row(Today.AddYears(-1), 0), maxAge, maxFailure, Today));
    }

    private static AssetLifecycleRow Row(DateOnly? purchaseDate, int ticketCount, int? failedTicketCount = null) => new()
    {
        AssetId = 1,
        AssetCode = "PC-001",
        AssetName = "Test Asset",
        CategoryId = 1,
        CategoryName = "Laptop",
        DepartmentId = 1,
        DepartmentName = "IT",
        PurchaseDate = purchaseDate,
        TicketCount = ticketCount,
        FailedTicketCount = failedTicketCount ?? 0,
    };
}
