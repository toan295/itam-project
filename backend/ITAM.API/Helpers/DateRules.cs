namespace ITAM.API.Helpers;

// Quy tắc ngày dùng chung cho validator (ngày mua, ngày phân bổ, ngày thu hồi không được ở tương lai).
public static class DateRules
{
    // Ngày muộn nhất còn hợp lệ. Server tính theo UTC còn người dùng ở UTC+7: sau 17:00 UTC "hôm nay" của người
    // dùng đã là ngày mai theo UTC, nên cho phép lệch +1 ngày để không từ chối nhầm ngày hôm nay của họ.
    public static DateOnly LatestAllowedDate() => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    public static bool IsNotInFuture(DateOnly date) => date <= LatestAllowedDate();
}
