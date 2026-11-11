using ClosedXML.Excel;

namespace ITAM.API.Reporting;

public static class ExcelReportRenderer
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaxSheetNameLength = 31;

    public static byte[] Render(ReportDocument document)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = document.Title;
        workbook.Properties.Author = "EAIMS";

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tables = document.Tables.Count > 0 ? document.Tables : new List<ReportTable>();
        if (tables.Count == 0)
        {
            tables = new List<ReportTable> { new("Báo cáo", Array.Empty<ReportColumn>(), Array.Empty<object?[]>()) };
        }

        var first = true;
        foreach (var table in tables)
        {
            var sheet = workbook.Worksheets.Add(UniqueSheetName(table.Name, usedNames));
            WriteSheet(sheet, document, table, includeHeaderBlock: first);
            first = false;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSheet(IXLWorksheet sheet, ReportDocument document, ReportTable table, bool includeHeaderBlock)
    {
        var row = 1;
        sheet.Cell(row, 1).Value = SpreadsheetSafe.Escape(includeHeaderBlock ? document.Title : table.Name);
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row++;

        if (includeHeaderBlock)
        {
            foreach (var (label, value) in document.Meta)
            {
                sheet.Cell(row, 1).Value = SpreadsheetSafe.Escape(label);
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = SpreadsheetSafe.Escape(value);
                row++;
            }

            foreach (var warning in document.Warnings)
            {
                sheet.Cell(row, 1).Value = SpreadsheetSafe.Escape("Lưu ý: " + warning);
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.DarkOrange;
                row++;
            }
        }

        row++;   // dòng trống trước bảng
        var headerRow = row;
        var columns = table.Columns;
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = SpreadsheetSafe.Escape(columns[c].Header);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        row = headerRow + 1;
        foreach (var data in table.Rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                WriteCell(sheet.Cell(row, c + 1), c < data.Length ? data[c] : null, columns[c].Kind);
            }

            row++;
        }

        if (columns.Count > 0 && table.Rows.Count > 0)
        {
            sheet.Range(headerRow, 1, row - 1, columns.Count).SetAutoFilter();
        }

        if (!string.IsNullOrEmpty(table.Note))
        {
            row++;
            sheet.Cell(row, 1).Value = SpreadsheetSafe.Escape(table.Note);
            sheet.Cell(row, 1).Style.Font.Italic = true;
        }

        for (var c = 0; c < columns.Count; c++)
        {
            var width = Math.Clamp(columns[c].Width * 12, 10, 60);
            sheet.Column(c + 1).Width = width;
        }

        sheet.SheetView.FreezeRows(headerRow);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }

    // Ghi đúng kiểu: số là số, ngày là ngày (dd/mm/yyyy), văn bản qua SpreadsheetSafe — không ép mọi thứ thành chuỗi.
    private static void WriteCell(IXLCell cell, object? value, ReportColumnKind kind)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                cell.Value = SpreadsheetSafe.Escape(text);
                cell.Style.Alignment.WrapText = true;
                return;
            case bool flag:
                cell.Value = flag ? "Có" : "Không";
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/mm/yyyy";
                return;
            case DateTime dateTime:
                if (kind == ReportColumnKind.DateTime)
                {
                    cell.Value = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).AddHours(7);   // giờ Việt Nam
                    cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                }
                else
                {
                    cell.Value = dateTime.Date;
                    cell.Style.DateFormat.Format = "dd/mm/yyyy";
                }

                return;
            case int or long or short or byte:
                cell.Value = Convert.ToInt64(value);
                cell.Style.NumberFormat.Format = "#,##0";
                return;
            case decimal or double or float:
                cell.Value = Convert.ToDecimal(value);
                cell.Style.NumberFormat.Format = kind switch
                {
                    ReportColumnKind.Money => "#,##0",
                    ReportColumnKind.Percent => "0.0%",
                    _ => "#,##0.00",
                };
                return;
            default:
                cell.Value = SpreadsheetSafe.Escape(value.ToString());
                return;
        }
    }

    // Tên sheet: tối đa 31 ký tự, không chứa : \ / ? * [ ], không trùng nhau (không phân biệt hoa/thường).
    internal static string UniqueSheetName(string name, HashSet<string> used)
    {
        var clean = new string(name.Where(ch => ":\\/?*[]".IndexOf(ch) < 0).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "Sheet";
        }

        if (clean.Length > MaxSheetNameLength)
        {
            clean = clean[..MaxSheetNameLength];
        }

        var candidate = clean;
        var n = 2;
        while (!used.Add(candidate))
        {
            var suffix = $" ({n++})";
            candidate = clean[..Math.Min(clean.Length, MaxSheetNameLength - suffix.Length)] + suffix;
        }

        return candidate;
    }
}
