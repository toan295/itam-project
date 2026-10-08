namespace ITAM.API.Models.Enums;

// Mã các bước chính của luồng thanh lý — do code điều khiển nên bất biến (Admin chỉ đổi được
// tên/màu/thứ tự hiển thị của các trạng thái hệ thống, không đổi được Code, không xoá được).
public static class DisposalStatusCodes
{
    public const string Inspected = "Inspected";   // Technician đã kiểm tra tài sản
    public const string Proposed = "Proposed";     // Technician đã đề xuất thanh lý, chờ Manager duyệt
    public const string Approved = "Approved";     // Manager đã duyệt, chờ Admin IT thực hiện
    public const string Rejected = "Rejected";     // Manager từ chối (trạng thái cuối)
    public const string Completed = "Completed";   // Admin IT đã thanh lý, tài sản chuyển Disposed (trạng thái cuối)

    public static readonly string[] FinalCodes = { Rejected, Completed };

    public static readonly string[] AllowedColors = { "success", "warning", "danger", "info", "slate" };
}
