namespace ITAM.API.Reporting;

public enum ReportFormat
{
    Excel,
    Pdf,
}

// Kiểu dữ liệu của cột: quyết định định dạng ô Excel (kiểu số/ngày thật, không phải chuỗi) và cách in ở PDF (vi-VN, dd/MM/yyyy).
public enum ReportColumnKind
{
    Text,
    Integer,
    Decimal,
    Money,      // VND, không lẻ
    Percent,    // giá trị 0..1
    Date,       // DateOnly / DateTime (chỉ ngày)
    DateTime,   // UTC, hiển thị giờ Việt Nam
}

public sealed record ReportColumn(string Header, ReportColumnKind Kind = ReportColumnKind.Text, double Width = 1);

// Một bảng trong báo cáo (Excel: một sheet; PDF: một khối có tiêu đề). Ô: string/int/long/decimal/double/bool/DateOnly/DateTime/null.
public sealed class ReportTable
{
    public ReportTable(string name, IReadOnlyList<ReportColumn> columns, IReadOnlyList<object?[]> rows)
    {
        Name = name;
        Columns = columns;
        Rows = rows;
    }

    public string Name { get; }                            // tên sheet / tiêu đề khối
    public IReadOnlyList<ReportColumn> Columns { get; }
    public IReadOnlyList<object?[]> Rows { get; }
    public string? Note { get; init; }                     // ghi chú dưới bảng (vd. "Tổng số dòng")
}

public sealed class ReportDocument
{
    public required string Title { get; init; }
    // Cặp nhãn–giá trị in ngay đầu báo cáo: người xuất, ngày xuất, bộ lọc đã dùng... để file đứng một mình vẫn hiểu được.
    public List<(string Label, string Value)> Meta { get; } = new();
    public List<ReportTable> Tables { get; } = new();
    public List<string> Warnings { get; } = new();          // cảnh báo hiển thị nổi bật (vd. "45 tài sản thiếu ngày mua")
    public int TotalRows => Tables.Sum(t => t.Rows.Count);
}

public sealed record ReportFile(byte[] Content, string ContentType, string FileName);

public class ReportTooLargeException : Exception
{
    public ReportTooLargeException(int limit)
        : base($"Báo cáo vượt giới hạn {limit:N0} dòng. Hãy thu hẹp bộ lọc (phòng ban, loại, năm) rồi xuất lại.")
    {
    }
}
