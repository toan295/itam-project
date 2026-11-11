namespace ITAM.API.Helpers;

// "Hôm nay" theo giờ Việt Nam (UTC+7), không phụ thuộc múi giờ máy chủ. Hạn license, cảnh báo sắp hết hạn tính theo
// ngày lịch Việt Nam: license hết hạn ngày D vẫn dùng được hết ngày D, sang 00:00 ngày D+1 (giờ VN) mới là "đã hết hạn".
public static class VietnamTime
{
    public static DateTime Now() => DateTime.UtcNow.AddHours(7);

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
}
