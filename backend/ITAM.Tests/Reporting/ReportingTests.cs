using System.Text;
using ClosedXML.Excel;
using ITAM.API.Reporting;

namespace ITAM.Tests.Reporting;

public class ReportingTests
{
    private static ReportDocument Sample()
    {
        var doc = new ReportDocument { Title = "Báo cáo tồn kho thiết bị" };
        doc.Meta.Add(("Người xuất", "EAIMS Admin"));
        doc.Meta.Add(("Bộ lọc", "Phòng ban: Phòng IT"));
        doc.Warnings.Add("45 tài sản chưa có ngày mua");
        doc.Tables.Add(new ReportTable(
            "Tồn kho",
            new[]
            {
                new ReportColumn("Mã", ReportColumnKind.Text, 1),
                new ReportColumn("Tên", ReportColumnKind.Text, 2),
                new ReportColumn("Ngày mua", ReportColumnKind.Date),
                new ReportColumn("Số lượng", ReportColumnKind.Integer),
                new ReportColumn("Đơn giá", ReportColumnKind.Money),
                new ReportColumn("Tỷ lệ lỗi", ReportColumnKind.Percent),
                new ReportColumn("Tạo lúc", ReportColumnKind.DateTime),
            },
            new List<object?[]>
            {
                new object?[] { "PC-001", "Máy tính Đặt bàn Ếch Ườn", new DateOnly(2024, 5, 3), 3, 20_000_000m, 0.125m, new DateTime(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc) },
                new object?[] { "=HYPERLINK(\"http://evil\")", "+cmd|' /C calc'!A0", null, 0, 0m, 0m, null },
            })
        { Note = "Tổng cộng: 2 dòng" });
        return doc;
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+84 90", "'+84 90")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tabc", "'\tabc")]
    [InlineData("Máy tính", "Máy tính")]
    [InlineData("a=b", "a=b")]
    [InlineData("", "")]
    public void Escape_PrefixesDangerousLeadingCharacters(string input, string expected)
    {
        Assert.Equal(expected, SpreadsheetSafe.Escape(input));
    }

    [Fact]
    public void Escape_NullAndControlCharacters_AreHandled()
    {
        Assert.Equal(string.Empty, SpreadsheetSafe.Escape(null));
        Assert.Equal("abc", SpreadsheetSafe.Escape("a\u0001b\u0000c"));
    }

    [Fact]
    public void Excel_RoundTrip_KeepsTypedValuesHeadersAndMeta()
    {
        var bytes = ExcelReportRenderer.Render(Sample());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Tồn kho");
        Assert.Equal("Báo cáo tồn kho thiết bị", sheet.Cell(1, 1).GetString());
        Assert.Equal("Người xuất", sheet.Cell(2, 1).GetString());
        Assert.Equal("EAIMS Admin", sheet.Cell(2, 2).GetString());
        Assert.Contains("45 tài sản chưa có ngày mua", sheet.Cell(4, 1).GetString());

        var header = sheet.Row(6);
        Assert.Equal("Mã", header.Cell(1).GetString());
        var data = sheet.Row(7);
        Assert.Equal("PC-001", data.Cell(1).GetString());
        Assert.Equal(new DateTime(2024, 5, 3), data.Cell(3).GetDateTime());          // ngày thật, không phải chuỗi
        Assert.Equal(3, data.Cell(4).GetDouble());
        Assert.Equal(20_000_000d, data.Cell(5).GetDouble());                         // tiền là số
        Assert.Equal(0.125d, data.Cell(6).GetDouble());
        Assert.Equal(new DateTime(2026, 10, 29, 0, 30, 0), data.Cell(7).GetDateTime());   // 17:30 UTC = 00:30 giờ VN hôm sau
        Assert.Equal("dd/mm/yyyy", data.Cell(3).Style.DateFormat.Format);
    }

    [Fact]
    public void Excel_UserTextStartingWithFormulaCharacters_IsNeverStoredAsFormula()
    {
        var bytes = ExcelReportRenderer.Render(Sample());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var cell = workbook.Worksheet("Tồn kho").Cell(8, 1);
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);                  // lưu là văn bản (ClosedXML chuyển dấu nháy đầu thành "quote prefix" của Excel)
        Assert.True(cell.Style.IncludeQuotePrefix);
        Assert.False(workbook.Worksheet("Tồn kho").Cell(8, 2).HasFormula);
    }

    [Fact]
    public void Excel_MultipleTables_BecomeSeparateSheetsWithUniqueNames()
    {
        var doc = Sample();
        doc.Tables.Add(new ReportTable("Tồn kho", new[] { new ReportColumn("A") }, new List<object?[]> { new object?[] { "x" } }));
        doc.Tables.Add(new ReportTable("Tên sheet rất dài vượt quá ba mươi mốt ký tự cho phép", new[] { new ReportColumn("A") }, Array.Empty<object?[]>()));
        doc.Tables.Add(new ReportTable("A:B/C?[x]*", new[] { new ReportColumn("A") }, Array.Empty<object?[]>()));

        using var workbook = new XLWorkbook(new MemoryStream(ExcelReportRenderer.Render(doc)));

        var names = workbook.Worksheets.Select(w => w.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => Assert.InRange(n.Length, 1, 31));
        Assert.DoesNotContain(names, n => n.IndexOfAny(new[] { ':', '\\', '/', '?', '*', '[', ']' }) >= 0);
    }

    [Fact]
    public void Excel_EmptyDocumentAndEmptyTable_StillProduceAValidWorkbook()
    {
        var empty = new ReportDocument { Title = "Rỗng" };
        using var a = new XLWorkbook(new MemoryStream(ExcelReportRenderer.Render(empty)));
        Assert.Single(a.Worksheets);

        var withEmptyTable = new ReportDocument { Title = "Rỗng" };
        withEmptyTable.Tables.Add(new ReportTable("Dữ liệu", new[] { new ReportColumn("Mã") }, Array.Empty<object?[]>()));
        using var b = new XLWorkbook(new MemoryStream(ExcelReportRenderer.Render(withEmptyTable)));
        Assert.Equal("Mã", b.Worksheet("Dữ liệu").Cell(3, 1).GetString());
    }

    [Fact]
    public void Pdf_WithVietnameseText_IsAValidPdfWithEmbeddedVietnameseFont()
    {
        var bytes = PdfReportRenderer.Render(Sample());

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 2_000);
        Assert.Contains("BeVietnamPro", Encoding.Latin1.GetString(bytes));   // font có dấu được nhúng, không dùng font hệ thống
    }

    [Fact]
    public void Pdf_ManyRows_SpansPagesWithoutThrowing()
    {
        var doc = new ReportDocument { Title = "Nhiều dòng" };
        var rows = Enumerable.Range(1, 800).Select(i => new object?[] { $"PC-{i:0000}", "Thiết bị số " + i, i * 1000m }).ToList();
        doc.Tables.Add(new ReportTable("Danh sách", new[] { new ReportColumn("Mã"), new ReportColumn("Tên", ReportColumnKind.Text, 3), new ReportColumn("Giá", ReportColumnKind.Money) }, rows));

        var bytes = PdfReportRenderer.Render(doc);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains("/Type /Pages", Encoding.Latin1.GetString(bytes).Replace("/Type/Pages", "/Type /Pages"));
    }

    [Fact]
    public void Pdf_EmptyTable_RendersMessageInsteadOfFailing()
    {
        var doc = new ReportDocument { Title = "Rỗng" };
        doc.Tables.Add(new ReportTable("Danh sách", new[] { new ReportColumn("Mã") }, Array.Empty<object?[]>()));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(PdfReportRenderer.Render(doc), 0, 4));
    }

    [Theory]
    [InlineData(20_000_000, ReportColumnKind.Money, "20.000.000 ₫")]
    [InlineData(0.125, ReportColumnKind.Percent, "12,5%")]
    [InlineData(1234, ReportColumnKind.Integer, "1.234")]
    public void Pdf_FormatCell_UsesVietnameseNumberFormats(double value, ReportColumnKind kind, string expected)
    {
        var raw = kind == ReportColumnKind.Integer ? (object)(int)value : (decimal)value;
        Assert.Equal(expected, PdfReportRenderer.FormatCell(raw, kind));
    }

    [Fact]
    public void Pdf_FormatCell_DatesAreDdMmYyyyAndDateTimeIsVietnamTime()
    {
        Assert.Equal("03/05/2024", PdfReportRenderer.FormatCell(new DateOnly(2024, 5, 3), ReportColumnKind.Date));
        Assert.Equal("29/10/2026 00:30", PdfReportRenderer.FormatCell(new DateTime(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc), ReportColumnKind.DateTime));
        Assert.Equal("=HYPERLINK(x)", PdfReportRenderer.FormatCell("=HYPERLINK(x)", ReportColumnKind.Text));   // PDF không cần dấu nháy
    }

    [Theory]
    [InlineData(null, ReportFormat.Excel)]
    [InlineData("", ReportFormat.Excel)]
    [InlineData("excel", ReportFormat.Excel)]
    [InlineData("XLSX", ReportFormat.Excel)]
    [InlineData(" pdf ", ReportFormat.Pdf)]
    public void ParseFormat_AcceptsKnownValues(string? input, ReportFormat expected)
    {
        Assert.Equal(expected, ReportExporter.ParseFormat(input));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("docx")]
    [InlineData("pdf;")]
    public void ParseFormat_RejectsUnknownValues(string input)
    {
        Assert.Throws<ArgumentException>(() => ReportExporter.ParseFormat(input));
    }

    [Fact]
    public void BuildFileName_IsAsciiSafeAndStamped()
    {
        var name = ReportExporter.BuildFileName("Tồn kho / ../../etc\r\nX", "xlsx", new DateTime(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc));

        Assert.Matches("^eaims-[a-z0-9-]+-20261029-003000\\.xlsx$", name);
        Assert.DoesNotContain("..", name);
        Assert.DoesNotContain("--", name);
        Assert.Equal("eaims-bao-cao-20261029-003000.pdf", ReportExporter.BuildFileName("???", "pdf", new DateTime(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Export_OverRowLimit_ThrowsBeforeGeneratingAnything()
    {
        var doc = new ReportDocument { Title = "Lớn" };
        var rows = Enumerable.Range(0, ReportExporter.MaxRows + 1).Select(_ => new object?[] { "x" }).ToList();
        doc.Tables.Add(new ReportTable("Dữ liệu", new[] { new ReportColumn("A") }, rows));

        var ex = Assert.Throws<ReportTooLargeException>(() => new ReportExporter().Export(doc, ReportFormat.Excel, "lon"));
        Assert.Contains("50.000", ex.Message.Replace(",", "."));
    }

    [Fact]
    public void Export_ReturnsCorrectContentTypeAndFileName()
    {
        var exporter = new ReportExporter();

        var xlsx = exporter.Export(Sample(), ReportFormat.Excel, "ton-kho");
        var pdf = exporter.Export(Sample(), ReportFormat.Pdf, "ton-kho");

        Assert.Equal(ExcelReportRenderer.ContentType, xlsx.ContentType);
        Assert.EndsWith(".xlsx", xlsx.FileName);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.EndsWith(".pdf", pdf.FileName);
        Assert.StartsWith("PK", Encoding.ASCII.GetString(xlsx.Content, 0, 2));   // xlsx là file zip
    }
}
