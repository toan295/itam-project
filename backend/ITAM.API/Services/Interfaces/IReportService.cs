using ITAM.API.Reporting;

namespace ITAM.API.Services.Interfaces;

// Người xuất + bộ lọc. Phạm vi phòng ban do service ép (Admin IT tuỳ chọn; vai trò khác bị khoá ở phòng ban của mình).
public sealed record ReportRequest(
    int UserId,
    string? Role,
    int? UserDepartmentId,
    int? DepartmentId = null,
    int? CategoryId = null,
    string? Status = null,
    int? PurchaseYear = null,
    string? Keyword = null,
    int? Months = null,
    int? Days = null);

// UC-19. Mỗi hàm trả mô hình báo cáo dùng chung cho Excel và PDF; số liệu lấy từ cùng service với màn hình (Vòng đời, Dự báo).
public interface IReportService
{
    Task<ReportDocument> BuildInventoryAsync(ReportRequest request);          // Tồn kho thiết bị
    Task<ReportDocument> BuildMaintenanceDueAsync(ReportRequest request);     // Sắp bảo trì: phiếu đang xử lý + bảo hành sắp hết
    Task<ReportDocument> BuildShortLifecycleAsync(ReportRequest request);     // Vòng đời ngắn: đã vượt ngưỡng + sắp tới hạn
    Task<ReportDocument> BuildLicensesAsync(ReportRequest request);           // Phần mềm & Giấy phép (lọc theo tình trạng; không theo phòng ban)
}
