namespace ITAM.API.Services.Implementations;

// Quy tắc chung cho đầu vào dự báo (kế hoạch nhân sự, định mức, đơn giá): mỗi bản ghi có Version; client phải gửi đúng phiên bản
// đang thấy khi sửa/xoá, hoặc null khi tạo mới. Hai Admin cùng sửa thì người đến sau nhận 409 thay vì ghi đè lặng lẽ.
internal static class ForecastInputGuard
{
    // exists: bản ghi đang tồn tại (chưa xoá). what: tên hiển thị trong thông báo.
    public static void EnsureVersion(bool exists, int? currentVersion, int? clientVersion, string what)
    {
        if (!exists)
        {
            if (clientVersion.HasValue)
            {
                throw new ForecastInputConflictException(
                    $"{what} đã bị xoá hoặc đặt lại bởi người khác. Vui lòng tải lại trang rồi thao tác lại.");
            }

            return;
        }

        if (!clientVersion.HasValue || clientVersion.Value != currentVersion)
        {
            throw new ForecastInputConflictException(
                $"{what} vừa được người khác tạo hoặc sửa. Vui lòng tải lại trang để xem giá trị mới nhất rồi thao tác lại.");
        }
    }

    public static void RequireVersionForDelete(int? version)
    {
        if (!version.HasValue)
        {
            throw new ArgumentException("Thiếu version của bản ghi cần xoá (tải lại trang để lấy bản mới nhất).");
        }
    }

    // MySQL datetime(6) giữ tới micro-giây: cắt sẵn để giá trị trả về lúc lưu trùng với giá trị đọc lại từ DB.
    public static DateTime NowUtc()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    public static DateTime? AsUtc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
