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

    // ---- Tuần 8, L1: điểm ưu tiên = round(50 × max(tỷ lệ tuổi, tỷ lệ lỗi)), 0..100 ----

    [Fact]
    public void ComputePriorityScore_ExactlyAtThresholds_Returns50()
    {
        Assert.Equal(50, LifecycleCalculator.ComputePriorityScore(5d, 5, 3, 3));
        Assert.Equal(50, LifecycleCalculator.ComputePriorityScore(null, 5, 3, 3));   // chỉ đúng ngưỡng lỗi
    }

    [Fact]
    public void ComputePriorityScore_DoubleTheFailureThreshold_Returns100()
    {
        Assert.Equal(100, LifecycleCalculator.ComputePriorityScore(null, 5, 6, 3));   // S13 của C.2.2
    }

    [Fact]
    public void ComputePriorityScore_FarBeyondThresholds_ClampedTo100()
    {
        Assert.Equal(100, LifecycleCalculator.ComputePriorityScore(30d, 5, 50, 3));
    }

    [Fact]
    public void ComputePriorityScore_NoPurchaseDate_UsesFailureRatioOnly()
    {
        Assert.Equal(17, LifecycleCalculator.ComputePriorityScore(null, 5, 1, 3));   // 50 × 1/3 = 16,7
        Assert.Equal(25, LifecycleCalculator.ComputePriorityScore(null, 5, 2, 4));
        Assert.Equal(0, LifecycleCalculator.ComputePriorityScore(null, 5, 0, 3));
    }

    [Fact]
    public void ComputePriorityScore_AgeRatioDominates_UsesMax()
    {
        // tuổi 6/5 = 1,2 lớn hơn lỗi 1/3 → 60 (không phải tổng/trung bình của hai tỷ lệ). S12 của C.2.2.
        Assert.Equal(60, LifecycleCalculator.ComputePriorityScore(6d, 5, 1, 3));
    }

    [Fact]
    public void ComputePriorityScore_FailureRatioDominates_UsesMax()
    {
        // lỗi 6/3 = 2,0 lớn hơn tuổi 2/5 = 0,4 → 100 (tuổi không được kéo điểm xuống).
        Assert.Equal(100, LifecycleCalculator.ComputePriorityScore(2d, 5, 6, 3));
    }

    [Fact]
    public void ComputePriorityScore_HalfPoint_RoundsAwayFromZero()
    {
        Assert.Equal(13, LifecycleCalculator.ComputePriorityScore(null, 5, 1, 4));   // 12,5 → 13 (không làm tròn chẵn về 12)
    }

    [Fact]
    public void ComputePriorityScore_NeverOutsideZeroToHundred()
    {
        foreach (var age in new double?[] { null, 0d, 0.1d, 5d, 12d, 400d })
        {
            foreach (var tickets in new[] { 0, 1, 3, 7, 1000 })
            {
                var score = LifecycleCalculator.ComputePriorityScore(age, 5, tickets, 3);
                Assert.InRange(score, 0, 100);
            }
        }
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(5, 0)]
    public void ComputePriorityScore_NonPositiveThreshold_ThrowsArgumentException(int maxAge, int maxFailure)
    {
        Assert.Throws<ArgumentException>(() => LifecycleCalculator.ComputePriorityScore(1d, maxAge, 1, maxFailure));
    }

    // ---- Tuần 8, L7: chuỗi lý do đúng từng ký tự (dấu phẩy thập phân kiểu vi-VN) ----

    [Fact]
    public void BuildReasonText_AgeOnly_MatchesExactFormat()
    {
        Assert.Equal("Quá tuổi: 6,0 năm (ngưỡng 5 năm)",
            LifecycleCalculator.BuildReasonText(true, false, 6d, 5, 0, 3));
    }

    [Fact]
    public void BuildReasonText_FailureOnly_MatchesExactFormat()
    {
        Assert.Equal("Lỗi nhiều: 6 phiếu bảo trì (ngưỡng 3 phiếu)",
            LifecycleCalculator.BuildReasonText(false, true, null, 5, 6, 3));
    }

    [Fact]
    public void BuildReasonText_Both_AgeBeforeFailureJoinedBySemicolon()
    {
        Assert.Equal("Quá tuổi: 8,4 năm (ngưỡng 5 năm); Lỗi nhiều: 4 phiếu bảo trì (ngưỡng 3 phiếu)",
            LifecycleCalculator.BuildReasonText(true, true, 8.4d, 5, 4, 3));
    }

    [Fact]
    public void BuildReasonText_Neither_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LifecycleCalculator.BuildReasonText(false, false, 1d, 5, 0, 3));
    }

    // ---- Tuần 8, L2: nhóm tuổi theo NGÀY LỊCH (không dùng 365,25) ----

    [Fact]
    public void GetAgeBucketIndex_NoPurchaseDate_ReturnsUnknownBucket()
    {
        Assert.Equal(4, LifecycleCalculator.GetAgeBucketIndex(null, Today));
    }

    [Fact]
    public void GetAgeBucketIndex_ExactlyOneYearAgo_IsInOneToThreeBucket()
    {
        // 365 ngày / 365,25 = 0,9993 < 1 nhưng theo lịch đã tròn 1 năm.
        Assert.Equal(1, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-1), Today));
    }

    [Fact]
    public void GetAgeBucketIndex_OneDayBeforeOneYear_IsInUnderOneBucket()
    {
        Assert.Equal(0, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-1).AddDays(1), Today));
    }

    [Fact]
    public void GetAgeBucketIndex_ThreeYearBoundary()
    {
        Assert.Equal(1, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-3).AddDays(1), Today));
        Assert.Equal(2, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-3), Today));
    }

    [Fact]
    public void GetAgeBucketIndex_ExactlyFiveYearsAgo_IsInFiveOrMoreBucket()
    {
        Assert.Equal(2, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-5).AddDays(1), Today));
        Assert.Equal(3, LifecycleCalculator.GetAgeBucketIndex(Today.AddYears(-5), Today));
    }

    [Fact]
    public void GetAgeBucketIndex_PurchaseDateInFuture_IsInUnderOneBucket()
    {
        Assert.Equal(0, LifecycleCalculator.GetAgeBucketIndex(Today.AddDays(40), Today));
    }

    // ---- Tuần 8, L3: sắp tới hạn ----

    [Fact]
    public void EvaluateUpcoming_AgeDueDateInsideWindow_IsAgeDueSoon()
    {
        var result = LifecycleCalculator.EvaluateUpcoming(Row(Today.AddYears(-5).AddMonths(3), 0), 5, 3, 6, Today);

        Assert.True(result.AgeDueSoon);
        Assert.False(result.NearFailureThreshold);
        Assert.Equal(new[] { LifecycleCalculator.ReasonAgeDueSoon }, result.Reasons);
        Assert.Equal(Today.AddMonths(3), result.AgeDueDate);
    }

    [Fact]
    public void EvaluateUpcoming_AgeDueDateExactlyAtWindowEnd_IsAgeDueSoon()
    {
        // cận trên cửa sổ BAO GỒM: ngày đủ tuổi đúng bằng hôm nay + 6 tháng.
        var result = LifecycleCalculator.EvaluateUpcoming(Row(Today.AddMonths(6).AddYears(-5), 0), 5, 3, 6, Today);

        Assert.True(result.AgeDueSoon);
        Assert.Equal(Today.AddMonths(6), result.AgeDueDate);
    }

    [Fact]
    public void EvaluateUpcoming_AgeDueDateAfterWindow_IsNotAgeDueSoon()
    {
        var result = LifecycleCalculator.EvaluateUpcoming(Row(Today.AddMonths(6).AddDays(1).AddYears(-5), 0), 5, 3, 6, Today);

        Assert.False(result.AgeDueSoon);
        Assert.Null(result.DaysUntilAgeDue);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void EvaluateUpcoming_AgeDueDateAlreadyPassed_IsNotAgeDueSoon()
    {
        // đã quá hạn (hoặc đúng hôm nay) thì thuộc "cần thay ngay", không thuộc "sắp tới".
        Assert.False(LifecycleCalculator.EvaluateUpcoming(Row(Today.AddYears(-6), 0), 5, 3, 12, Today).AgeDueSoon);
        Assert.False(LifecycleCalculator.EvaluateUpcoming(Row(Today.AddYears(-5), 0), 5, 3, 12, Today).AgeDueSoon);
    }

    [Fact]
    public void EvaluateUpcoming_AgeDue_ReasonTextAndDaysAreExact()
    {
        var today = new DateOnly(2026, 10, 5);
        var result = LifecycleCalculator.EvaluateUpcoming(Row(new DateOnly(2021, 11, 4), 0), 5, 3, 6, today);

        Assert.Equal(30, result.DaysUntilAgeDue);
        Assert.Equal("Sắp đủ tuổi thay thế: còn 30 ngày (ngày 04/11/2026)", result.ReasonText);
    }

    [Fact]
    public void EvaluateUpcoming_TicketCountOneBelowThreshold_IsNearFailure()
    {
        var result = LifecycleCalculator.EvaluateUpcoming(Row(null, 2), 5, 3, 12, Today);

        Assert.False(result.AgeDueSoon);
        Assert.True(result.NearFailureThreshold);
        Assert.Equal(new[] { LifecycleCalculator.ReasonNearFailureThreshold }, result.Reasons);
        Assert.Equal("Sát ngưỡng lỗi: 2/3 phiếu bảo trì", result.ReasonText);
        Assert.Null(result.DaysUntilAgeDue);
    }

    [Fact]
    public void EvaluateUpcoming_ZeroTicketsAndThresholdOne_IsNotNearFailure()
    {
        // khoá điều kiện TicketCount >= 1: với ngưỡng 1, mọi tài sản 0 phiếu KHÔNG được coi là "sát ngưỡng".
        Assert.False(LifecycleCalculator.EvaluateUpcoming(Row(null, 0), 5, 1, 12, Today).NearFailureThreshold);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void EvaluateUpcoming_TicketCountNotExactlyOneBelow_IsNotNearFailure(int tickets)
    {
        Assert.False(LifecycleCalculator.EvaluateUpcoming(Row(null, tickets), 5, 3, 12, Today).NearFailureThreshold);
    }

    [Fact]
    public void EvaluateUpcoming_Both_ReasonsAgeBeforeFailureJoinedBySemicolon()
    {
        var result = LifecycleCalculator.EvaluateUpcoming(Row(Today.AddYears(-5).AddMonths(3), 2), 5, 3, 6, Today);

        Assert.Equal(new[] { LifecycleCalculator.ReasonAgeDueSoon, LifecycleCalculator.ReasonNearFailureThreshold }, result.Reasons);
        Assert.Contains("; Sát ngưỡng lỗi: 2/3 phiếu bảo trì", result.ReasonText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void EvaluateUpcoming_MonthsOutsideRange_ThrowsArgumentException(int months)
    {
        Assert.Throws<ArgumentException>(() =>
            LifecycleCalculator.EvaluateUpcoming(Row(null, 0), 5, 3, months, Today));
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
