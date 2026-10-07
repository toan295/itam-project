using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Services.Implementations;

public static class LifecycleCalculator
{
    public static double? ComputeAgeYears(DateOnly? purchaseDate, DateOnly today)
    {
        if (!purchaseDate.HasValue)
        {
            return null;
        }

        var elapsedDays = today.DayNumber - purchaseDate.Value.DayNumber;
        return Math.Max(0, elapsedDays / 365.25d);
    }

    public static LifecycleEvaluation Evaluate(
        AssetLifecycleRow row,
        int maxAgeYears,
        int maxFailureCount,
        DateOnly today)
    {
        if (maxAgeYears < 1)
        {
            throw new ArgumentException("Ngưỡng tuổi tối đa phải lớn hơn 0.", nameof(maxAgeYears));
        }

        if (maxFailureCount < 1)
        {
            throw new ArgumentException("Ngưỡng số lần lỗi tối đa phải lớn hơn 0.", nameof(maxFailureCount));
        }

        var ageDueDate = row.PurchaseDate?.AddYears(maxAgeYears);
        var ageOverdueNow = ageDueDate.HasValue && ageDueDate.Value <= today;
        var failureOverdueNow = row.FailedTicketCount >= maxFailureCount;

        int? dueYear = null;
        var reasons = new List<string>();

        if (failureOverdueNow)
        {
            dueYear = today.Year;
            reasons.Add("Lỗi nhiều");
        }

        if (ageDueDate.HasValue)
        {
            if (ageOverdueNow)
            {
                dueYear = dueYear.HasValue
                    ? Math.Min(dueYear.Value, ageDueDate.Value.Year)
                    : ageDueDate.Value.Year;
                reasons.Insert(0, "Quá tuổi");
            }
            else if (!dueYear.HasValue)
            {
                // Dùng cho hợp đồng dự báo: tài sản chưa quá tuổi hôm nay vẫn có năm đến hạn.
                dueYear = ageDueDate.Value.Year;
                reasons.Add("Quá tuổi");
            }
        }

        return new LifecycleEvaluation
        {
            DueYear = dueYear,
            IsOverdueNow = ageOverdueNow || failureOverdueNow,
            Priority = ageOverdueNow && failureOverdueNow ? 2 : dueYear.HasValue ? 1 : 0,
            Reasons = reasons,
        };
    }
}
