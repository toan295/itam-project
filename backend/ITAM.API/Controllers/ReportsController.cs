using System.Security.Claims;
using ITAM.API.Authorization;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs;
using ITAM.API.Reporting;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// UC-19: xuất báo cáo Excel/PDF. Admin IT: toàn hệ thống (lọc được phòng ban); vai trò khác: luôn khoá ở phòng ban của mình.
// Mọi lần xuất (kể cả bị từ chối/lỗi) được AuditActionFilter ghi nhật ký (đối tượng "Report", hành động "Export").
[ApiController]
[Route("api/v1/reports")]
[HasPermission(Permissions.ReportsExport)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly IReportExporter _exporter;

    public ReportsController(IReportService reports, IReportExporter exporter)
    {
        _reports = reports;
        _exporter = exporter;
    }

    // Tồn kho thiết bị. format = excel | pdf (mặc định excel).
    [HttpGet("assets/export")]
    public Task<IActionResult> ExportInventory(
        [FromQuery] string? format, [FromQuery] int? departmentId, [FromQuery] int? categoryId, [FromQuery] string? status,
        [FromQuery] int? purchaseYear, [FromQuery] string? keyword) =>
        Export(format, ExportFileNames.Inventory,
            new ReportRequest(UserId(), Role(), UserDepartmentId(), departmentId, categoryId, status, purchaseYear, keyword),
            _reports.BuildInventoryAsync);

    // Phiếu bảo trì đang xử lý + thiết bị có bảo hành sắp hết (days: 1–365, mặc định 90).
    [HttpGet("maintenance-due/export")]
    public Task<IActionResult> ExportMaintenanceDue(
        [FromQuery] string? format, [FromQuery] int? departmentId, [FromQuery] int? categoryId, [FromQuery] int? days) =>
        Export(format, ExportFileNames.MaintenanceDue,
            new ReportRequest(UserId(), Role(), UserDepartmentId(), departmentId, categoryId, Days: days),
            _reports.BuildMaintenanceDueAsync);

    // Thiết bị cần / sắp thay thế (months: 1–60, mặc định 12).
    [HttpGet("short-lifecycle/export")]
    public Task<IActionResult> ExportShortLifecycle(
        [FromQuery] string? format, [FromQuery] int? departmentId, [FromQuery] int? categoryId, [FromQuery] int? months) =>
        Export(format, ExportFileNames.ShortLifecycle,
            new ReportRequest(UserId(), Role(), UserDepartmentId(), departmentId, categoryId, Months: months),
            _reports.BuildShortLifecycleAsync);

    // Phần mềm & Giấy phép (toàn hệ thống, không theo phòng ban): cần thêm quyền Quản lý giấy phép. status = expired | expiring | valid (bỏ trống = tất cả).
    [HttpGet("licenses/export")]
    [HasPermission(Permissions.LicensesManage)]
    public Task<IActionResult> ExportLicenses([FromQuery] string? format, [FromQuery] string? status) =>
        Export(format, ExportFileNames.Licenses,
            new ReportRequest(UserId(), Role(), UserDepartmentId(), Status: status),
            _reports.BuildLicensesAsync);

    private async Task<IActionResult> Export(string? format, string slug, ReportRequest request, Func<ReportRequest, Task<ReportDocument>> build)
    {
        try
        {
            var parsed = ReportExporter.ParseFormat(format);   // kiểm tra định dạng TRƯỚC khi truy vấn
            var document = await build(request);
            var file = _exporter.Export(document, parsed, slug);

            Response.Headers.CacheControl = "no-store";         // báo cáo chứa dữ liệu nội bộ: không cho cache ở trình duyệt/proxy
            Response.Headers["X-Report-Rows"] = document.TotalRows.ToString();
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (DepartmentForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex) when (ex is ArgumentException or ReportTooLargeException)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private string? Role() => User.FindFirstValue(ClaimTypes.Role);
    private int UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private int? UserDepartmentId() => int.TryParse(User.FindFirstValue("DepartmentId"), out var id) ? id : null;
}
