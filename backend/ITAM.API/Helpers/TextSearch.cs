using System.Globalization;
using System.Text;

namespace ITAM.API.Helpers;

// Tìm kiếm tiếng Việt cho lọc trong bộ nhớ: không phân biệt hoa/thường, dấu (máy = may, Đ = d) và khoảng trắng thừa.
public static class TextSearch
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            builder.Append(ch switch { 'đ' or 'Đ' => 'd', _ => char.ToLowerInvariant(ch) });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool Contains(string? haystack, string normalizedNeedle) =>
        normalizedNeedle.Length == 0 || Normalize(haystack).Contains(normalizedNeedle, StringComparison.Ordinal);
}
