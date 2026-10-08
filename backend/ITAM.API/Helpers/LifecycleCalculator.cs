using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Helpers;

// Lõi tính toán thuần (không DB, không đồng hồ hệ thống — `today` do người gọi truyền vào) cho D1–D5
// của ke-hoach-tuan7-lan2.md. Kết quả là nền tảng của dự báo ngân sách (UC-17) nên mọi thay đổi ở đây
// phải khớp hợp đồng PHẦN C.1.
public static class LifecycleCalculator
{
    // Tên lý do là hợp đồng với module Dự báo ngân sách và frontend — cố định, KHÔNG dịch.
    public const string ReasonAgeExceeded = "AgeExceeded";
    public const string ReasonFailureCountExceeded = "FailureCountExceeded";

    private const double DaysPerYear = 365.25;

    // D1: tài sản không có PurchaseDate → null (không suy diễn); PurchaseDate ở tương lai → tuổi 0.
    // Chỉ dùng để hiển thị/thống kê, KHÔNG dùng để so với ngưỡng (xem Evaluate).
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
        if (maxAgeYears < 1)
        {
            throw new ArgumentException("Ngưỡng tuổi tối đa phải lớn hơn 0.", nameof(maxAgeYears));
        }

        if (maxFailureCount < 1)
        {
            throw new ArgumentException("Ngưỡng số lần lỗi tối đa phải lớn hơn 0.", nameof(maxFailureCount));
        }

        // D3: cả hai so sánh đều BAO GỒM dấu bằng. Tuổi so theo ngày lịch (AddYears) thay vì số thực
        // 365.25 để mốc "tròn đúng N năm" luôn tính là đủ tuổi.
        var ageDueDate = row.PurchaseDate?.AddYears(maxAgeYears);
        var ageExceeded = ageDueDate.HasValue && ageDueDate.Value <= today;

        // D2: số lần lỗi = TỔNG số phiếu bảo trì mọi trạng thái (FailedTicketCount chỉ là thông tin phụ).
        var failureExceeded = row.TicketCount >= maxFailureCount;

        // D5: DueYear không bao giờ nhỏ hơn năm hiện tại — tài sản đã quá hạn từ lâu vẫn là "cần thay ngay
        // trong năm nay", nếu trả năm quá khứ thì dự báo của năm hiện tại sẽ bỏ sót nó.
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
}
