using System.Globalization;
using System.Text;

namespace ITAM.API.Helpers;

/// <summary>
/// QUY TẮC ĐẶT TÊN FILE XUẤT RA — dùng chung cho mọi chức năng tải file (báo cáo Excel/PDF, sao lưu dữ liệu...):
/// <c>eaims-{tên-chức-năng}-{yyyyMMdd-HHmmss}.{đuôi}</c>
/// <list type="bullet">
/// <item>Tiền tố cố định <c>eaims</c> để nhận ra file của hệ thống.</item>
/// <item>Tên chức năng viết thường, không dấu, ngăn cách bằng dấu gạch ngang (xem các hằng bên dưới).</item>
/// <item>Thời điểm xuất theo giờ Việt Nam, định dạng năm-tháng-ngày-giờ-phút-giây (sắp theo tên là sắp theo thời gian).</item>
/// <item>Chỉ gồm a-z, 0-9 và '-' nên an toàn trên mọi hệ điều hành và trong header tải file.</item>
/// </list>
/// Ví dụ: <c>eaims-ton-kho-thiet-bi-20261111-143005.xlsx</c>, <c>eaims-sao-luu-20261111-143005.sql</c>.
/// </summary>
public static class ExportFileNames
{
    public const string Prefix = "eaims";

    // Tên chức năng của từng loại file — thêm chức năng xuất mới thì khai báo ở đây để mọi nơi dùng chung một quy tắc.
    public const string Inventory = "ton-kho-thiet-bi";
    public const string MaintenanceDue = "sap-bao-tri";
    public const string ShortLifecycle = "vong-doi-ngan";
    public const string Licenses = "phan-mem-giay-phep";
    public const string Backup = "sao-luu";

    public static string Build(string slug, string extension, DateTime? utcNow = null, string? tag = null)
    {
        var name = Sanitize(slug);
        if (name.Length == 0) name = "bao-cao";
        var stamp = (utcNow ?? DateTime.UtcNow).AddHours(7).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = string.IsNullOrWhiteSpace(tag) ? string.Empty : "-" + Sanitize(tag);
        return $"{Prefix}-{name}-{stamp}{suffix}.{extension.TrimStart('.').ToLowerInvariant()}";
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            var safe = ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-';
            if (safe == '-' && builder.Length > 0 && builder[^1] == '-') continue;   // gộp nhiều ký tự lạ liên tiếp thành một dấu '-'
            builder.Append(safe);
        }

        return builder.ToString().Trim('-');
    }
}
