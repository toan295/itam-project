using System.Text;

namespace ITAM.API.Reporting;

// Chống "formula injection" (CSV/Excel injection): chuỗi do người dùng nhập (tên tài sản, ghi chú...) bắt đầu bằng = + - @ hoặc
// tab/xuống dòng có thể bị phần mềm bảng tính hiểu là công thức khi người khác mở file. Thêm dấu nháy đơn đứng đầu để luôn là văn bản.
public static class SpreadsheetSafe
{
    private static readonly char[] DangerousLeading = { '=', '+', '-', '@', '\t', '\r', '\n' };

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var clean = RemoveControlCharacters(value);
        return clean.Length > 0 && Array.IndexOf(DangerousLeading, clean[0]) >= 0 ? "'" + clean : clean;
    }

    // Ký tự điều khiển (trừ xuống dòng, tab) làm hỏng file xlsx (XML không cho phép) hoặc PDF.
    private static string RemoveControlCharacters(string value)
    {
        StringBuilder? builder = null;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            var bad = char.IsControl(ch) && ch is not ('\t' or '\r' or '\n');
            if (bad && builder is null)
            {
                builder = new StringBuilder(value, 0, i, value.Length);
            }

            if (!bad)
            {
                builder?.Append(ch);
            }
        }

        return builder?.ToString() ?? value;
    }
}
