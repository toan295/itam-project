using System.Globalization;
using System.Text.RegularExpressions;

namespace ITAM.API.Helpers;

// Chuẩn hoá họ tên để cùng một người không bị lưu thành nhiều kiểu ("nguyễn  văn an", "NGUYỄN VĂN AN"...).
public static class PersonNameNormalizer
{
    private static readonly CultureInfo Vietnamese = new("vi-VN");

    public static string Normalize(string name)
    {
        var collapsed = Regex.Replace(name.Trim(), @"\s+", " ");

        // Chỉ tự viết hoa chữ cái đầu mỗi từ khi người nhập gõ toàn thường hoặc toàn HOA; tên đã viết đúng giữ nguyên.
        if (collapsed == collapsed.ToLower(Vietnamese) || collapsed == collapsed.ToUpper(Vietnamese))
        {
            return Vietnamese.TextInfo.ToTitleCase(collapsed.ToLower(Vietnamese));
        }

        return collapsed;
    }
}
