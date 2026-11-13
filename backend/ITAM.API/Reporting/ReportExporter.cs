using ITAM.API.Helpers;

namespace ITAM.API.Reporting;

public interface IReportExporter
{
    // Sinh file từ mô hình báo cáo. Vượt giới hạn dòng -> ReportTooLargeException (kiểm tra TRƯỚC khi tốn công sinh file).
    ReportFile Export(ReportDocument document, ReportFormat format, string fileSlug);
}

public class ReportExporter : IReportExporter
{
    public const int MaxRows = 50_000;

    public ReportFile Export(ReportDocument document, ReportFormat format, string fileSlug)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.TotalRows > MaxRows)
        {
            throw new ReportTooLargeException(MaxRows);
        }

        return format switch
        {
            ReportFormat.Excel => new ReportFile(ExcelReportRenderer.Render(document), ExcelReportRenderer.ContentType, BuildFileName(fileSlug, "xlsx")),
            ReportFormat.Pdf => new ReportFile(PdfReportRenderer.Render(document), PdfReportRenderer.ContentType, BuildFileName(fileSlug, "pdf")),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    // "excel"/"xlsx"/"pdf" (không phân biệt hoa/thường); thiếu -> excel; giá trị khác -> ArgumentException (400).
    public static ReportFormat ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return ReportFormat.Excel;
        }

        return format.Trim().ToLowerInvariant() switch
        {
            "excel" or "xlsx" => ReportFormat.Excel,
            "pdf" => ReportFormat.Pdf,
            _ => throw new ArgumentException("format không hợp lệ. Phải là một trong: excel, pdf"),
        };
    }

    // Tên file theo quy tắc chung của hệ thống (xem ExportFileNames): eaims-{tên-chức-năng}-{yyyyMMdd-HHmmss}.{đuôi}
    public static string BuildFileName(string slug, string extension, DateTime? utcNow = null) =>
        ExportFileNames.Build(slug, extension, utcNow);
}
