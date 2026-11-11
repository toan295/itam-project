using System.Globalization;
using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ITAM.API.Reporting;

public static class PdfReportRenderer
{
    public const string ContentType = "application/pdf";
    private const string FontFamily = "Be Vietnam Pro";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly object InitLock = new();
    private static bool _initialized;

    // QuestPDF Community: miễn phí cho tổ chức/cá nhân doanh thu hằng năm dưới 1 triệu USD (xem điều khoản giấy phép của QuestPDF).
    // Font nhúng sẵn trong assembly nên không phụ thuộc font cài trên máy chủ (Linux/Docker thường thiếu font tiếng Việt).
    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resource in new[] { "Reporting.Fonts.BeVietnamPro-Regular.ttf", "Reporting.Fonts.BeVietnamPro-Bold.ttf" })
            {
                using var stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Thiếu font nhúng '{resource}'.");
                FontManager.RegisterFont(stream);
            }

            _initialized = true;
        }
    }

    public static byte[] Render(ReportDocument document)
    {
        EnsureInitialized();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(8));

            page.Header().Column(column =>
            {
                column.Item().Text(document.Title).FontSize(15).Bold().FontColor("#1F4E79");
                foreach (var (label, value) in document.Meta)
                {
                    column.Item().Text(text =>
                    {
                        text.Span(label + ": ").Bold();
                        text.Span(value);
                    });
                }

                foreach (var warning in document.Warnings)
                {
                    column.Item().Text("Lưu ý: " + warning).FontColor("#B45F06");
                }

                column.Item().PaddingBottom(6);
            });

            page.Content().Column(column =>
            {
                foreach (var table in document.Tables)
                {
                    column.Item().EnsureSpace(90).PaddingTop(6).Text(table.Name).FontSize(11).Bold();
                    column.Item().PaddingTop(3).Element(c => RenderTable(c, table));
                    if (!string.IsNullOrEmpty(table.Note))
                    {
                        column.Item().PaddingTop(3).Text(table.Note).Italic();
                    }
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor("#666666"));
                text.Span("EAIMS · Trang ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void RenderTable(IContainer container, ReportTable table)
    {
        if (table.Columns.Count == 0)
        {
            container.Text("Không có dữ liệu.");
            return;
        }

        container.Table(grid =>
        {
            grid.ColumnsDefinition(columns =>
            {
                foreach (var column in table.Columns)
                {
                    columns.RelativeColumn((float)Math.Max(0.4, column.Width));
                }
            });

            grid.Header(header =>
            {
                foreach (var column in table.Columns)
                {
                    header.Cell().Background("#1F4E79").Padding(3)
                        .Text(column.Header).Bold().FontColor(Colors.White);
                }
            });

            if (table.Rows.Count == 0)
            {
                grid.Cell().ColumnSpan((uint)table.Columns.Count).Padding(6).Text("Không có dữ liệu.").Italic();
                return;
            }

            for (var r = 0; r < table.Rows.Count; r++)
            {
                var background = r % 2 == 0 ? "#FFFFFF" : "#F2F6FA";
                var data = table.Rows[r];
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    var kind = table.Columns[c].Kind;
                    var cell = grid.Cell().Background(background).BorderBottom(0.25f).BorderColor("#D9D9D9").Padding(3);
                    var text = FormatCell(c < data.Length ? data[c] : null, kind);
                    if (kind is ReportColumnKind.Text or ReportColumnKind.Date or ReportColumnKind.DateTime)
                    {
                        cell.Text(text);
                    }
                    else
                    {
                        cell.AlignRight().Text(text);
                    }
                }
            }
        });
    }

    internal static string FormatCell(object? value, ReportColumnKind kind) => value switch
    {
        null => string.Empty,
        string text => SpreadsheetSafe.Escape(text).TrimStart('\''),   // PDF không phải bảng tính: không cần dấu nháy chống công thức
        bool flag => flag ? "Có" : "Không",
        DateOnly date => date.ToString("dd/MM/yyyy", Vi),
        DateTime dateTime => kind == ReportColumnKind.DateTime
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm", Vi)
            : dateTime.ToString("dd/MM/yyyy", Vi),
        int or long or short or byte => Convert.ToInt64(value).ToString("N0", Vi),
        decimal or double or float => kind switch
        {
            ReportColumnKind.Money => Convert.ToDecimal(value).ToString("N0", Vi) + " ₫",
            ReportColumnKind.Percent => (Convert.ToDecimal(value) * 100m).ToString("0.0", Vi) + "%",
            _ => Convert.ToDecimal(value).ToString("N2", Vi),
        },
        _ => value.ToString() ?? string.Empty,
    };
}
