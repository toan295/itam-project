using System.Globalization;
using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Helpers;

// Lõi tính toán thuần: không DB, không đồng hồ hệ thống; mọi hàm phụ thuộc ngày đều nhận `today`.
public static class LifecycleCalculator
{
    // Mã lý do là hợp đồng với frontend/module khác — cố định, không dịch.
    public const string ReasonAgeExceeded = "AgeExceeded";
    public const string ReasonFailureCountExceeded = "FailureCountExceeded";
    public const string ReasonAgeDueSoon = "AgeDueSoon";
    public const string ReasonNearFailureThreshold = "NearFailureThreshold";

    private const double DaysPerYear = 365.25;
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    // Chỉ dùng để hiển thị/thống kê/xếp hạng; quyết định vượt ngưỡng tuổi phải dùng ngày lịch trong Evaluate.
    public static double? ComputeAgeYears(DateOnly? purchaseDate, DateOnly today)
    {
        if (!purchaseDate.HasValue)
        {
            return null;
        }

        return Math.Max(0d, (today.DayNumber - purchaseDate.Value.DayNumber) / DaysPerYear);
    }

    public static LifecycleEvaluation Evaluate(
        AssetLifecycleRow row,
        int maxAgeYears,
        int maxFailureCount,
        DateOnly today)
    {
        ValidateThresholds(maxAgeYears, maxFailureCount);

        var ageDueDate = row.PurchaseDate?.AddYears(maxAgeYears);
        var ageExceeded = ageDueDate.HasValue && ageDueDate.Value <= today;

        // Tuần 7 đã chốt "số lần lỗi" theo tổng số phiếu bảo trì, FailedTicketCount chỉ là thông tin phụ.
        var failureExceeded = row.TicketCount >= maxFailureCount;

        int? dueYear =
            failureExceeded ? today.Year
            : ageDueDate.HasValue ? Math.Max(today.Year, ageDueDate.Value.Year)
            : null;

        var reasons = new List<string>(2);
        if (ageExceeded)
        {
            reasons.Add(ReasonAgeExceeded);
        }

        if (failureExceeded)
        {
            reasons.Add(ReasonFailureCountExceeded);
        }

        return new LifecycleEvaluation
        {
            DueYear = dueYear,
            IsOverdueNow = ageExceeded || failureExceeded,
            IsHighPriority = ageExceeded && failureExceeded,
            Reasons = reasons,
        };
    }

    // L1: điểm ưu tiên 0–100 chỉ để XẾP HẠNG, không dùng để quyết định tài sản có phải ứng viên hay không
    // (việc đó do Evaluate — ngày lịch — quyết định). severity = max(tỷ lệ tuổi, tỷ lệ lỗi), KHÔNG chặn trần ở 1:
    // đúng ngưỡng ⇒ 50, gấp đôi ngưỡng trở lên ⇒ 100. ageYears là số thực (chia 365,25) nên chỉ dùng để xếp hạng.
    public static int ComputePriorityScore(
        double? ageYears,
        int maxAgeYears,
        int ticketCount,
        int maxFailureCount)
    {
        ValidateThresholds(maxAgeYears, maxFailureCount);

        var ageRatio = ageYears.HasValue ? ageYears.Value / maxAgeYears : 0d;
        var failureRatio = (double)ticketCount / maxFailureCount;
        var severity = Math.Max(ageRatio, failureRatio);

        var rounded = Math.Round(50d * severity, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(rounded, 0d, 100d);
    }

    // L7: chuỗi lý do đúng khuôn (số thập phân dùng dấu phẩy kiểu vi-VN). Cờ vượt ngưỡng lấy từ Evaluate
    // (một nơi duy nhất quyết định) — hàm này không tự đánh giá lại.
    public static string BuildReasonText(
        bool ageExceeded,
        bool failureExceeded,
        double? ageYears,
        int maxAgeYears,
        int ticketCount,
        int maxFailureCount)
    {
        var parts = new List<string>(2);
        if (ageExceeded)
        {
            parts.Add($"Quá tuổi: {(ageYears ?? 0d).ToString("0.0", ViCulture)} năm (ngưỡng {maxAgeYears} năm)");
        }

        if (failureExceeded)
        {
            parts.Add($"Lỗi nhiều: {ticketCount} phiếu bảo trì (ngưỡng {maxFailureCount} phiếu)");
        }

        return string.Join("; ", parts);
    }

    public static LifecycleUpcomingEvaluation EvaluateUpcoming(
        AssetLifecycleRow row,
        int maxAgeYears,
        int maxFailureCount,
        int months,
        DateOnly today)
    {
        ValidateThresholds(maxAgeYears, maxFailureCount);

        if (months is < 1 or > 60)
        {
            throw new ArgumentException("Số tháng cảnh báo phải từ 1 đến 60.", nameof(months));
        }

        var ageDueDate = row.PurchaseDate?.AddYears(maxAgeYears);
        var windowEnd = today.AddMonths(months);
        var ageDueSoon = ageDueDate.HasValue
            && ageDueDate.Value > today
            && ageDueDate.Value <= windowEnd;

        var daysUntilAgeDue = ageDueSoon
            ? ageDueDate!.Value.DayNumber - today.DayNumber
            : (int?)null;

        // Cảnh báo khi còn đúng một phiếu nữa sẽ chạm ngưỡng. Với ngưỡng 1, không cảnh báo mọi tài sản 0 phiếu.
        var nearFailureThreshold = maxFailureCount > 1
            && row.TicketCount == maxFailureCount - 1;

        var reasonCodes = new List<string>(2);
        var reasonParts = new List<string>(2);

        if (ageDueSoon)
        {
            reasonCodes.Add(ReasonAgeDueSoon);
            reasonParts.Add(
                $"Sắp đủ tuổi thay thế: còn {daysUntilAgeDue} ngày (ngày {ageDueDate!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})");
        }

        if (nearFailureThreshold)
        {
            reasonCodes.Add(ReasonNearFailureThreshold);
            reasonParts.Add($"Sát ngưỡng lỗi: {row.TicketCount}/{maxFailureCount} phiếu bảo trì");
        }

        return new LifecycleUpcomingEvaluation
        {
            AgeDueSoon = ageDueSoon,
            NearFailureThreshold = nearFailureThreshold,
            AgeDueDate = ageDueDate,
            DaysUntilAgeDue = daysUntilAgeDue,
            Reasons = reasonCodes,
            ReasonText = string.Join("; ", reasonParts),
        };
    }

    // 0: <1 năm; 1: 1–3; 2: 3–5; 3: >=5; 4: không rõ ngày mua.
    // So theo ngày lịch để biên đúng tròn 1/3/5 năm không phụ thuộc 365.25.
    public static int GetAgeBucketIndex(DateOnly? purchaseDate, DateOnly today)
    {
        if (!purchaseDate.HasValue)
        {
            return 4;
        }

        if (purchaseDate.Value.AddYears(1) > today)
        {
            return 0;
        }

        if (purchaseDate.Value.AddYears(3) > today)
        {
            return 1;
        }

        if (purchaseDate.Value.AddYears(5) > today)
        {
            return 2;
        }

        return 3;
    }

    private static void ValidateThresholds(int maxAgeYears, int maxFailureCount)
    {
        if (maxAgeYears < 1)
        {
            throw new ArgumentException("Ngưỡng tuổi tối đa phải lớn hơn 0.", nameof(maxAgeYears));
        }

        if (maxFailureCount < 1)
        {
            throw new ArgumentException("Ngưỡng số lần lỗi tối đa phải lớn hơn 0.", nameof(maxFailureCount));
        }
    }
}
