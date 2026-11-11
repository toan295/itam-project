using System.Text.RegularExpressions;

namespace ITAM.API.Helpers;

// Số điện thoại Việt Nam: chấp nhận nhập có dấu cách, chấm, gạch ngang, ngoặc; lưu dạng đã chuẩn hoá (chỉ chữ số, có thể bắt đầu bằng +).
// Hợp lệ: 0 hoặc +84 theo sau 9–10 chữ số (di động 10 số, máy bàn 10–11 số).
public static class PhoneNumber
{
    private static readonly Regex Valid = new(@"^(\+84|0)\d{9,10}$", RegexOptions.Compiled);
    private static readonly Regex Separators = new(@"[\s.\-()]", RegexOptions.Compiled);

    public const string InvalidMessage = "Số điện thoại không hợp lệ (vd 0912345678 hoặc +84912345678).";

    // Rỗng/khoảng trắng -> null (không có số điện thoại).
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return Separators.Replace(input.Trim(), "");
    }

    public static bool IsValid(string? input)
    {
        var normalized = Normalize(input);
        return normalized is not null && Valid.IsMatch(normalized);
    }
}
