namespace ITAM.API.Repositories.Models;

// Bộ lọc bổ sung cho nhật ký hệ thống (Service đã validate).
// Keyword: tìm trong hành động, đối tượng, lý do, yêu cầu, giá trị cũ/mới, mã yêu cầu và họ tên/email người thực hiện.
// Sort: newest (mặc định) | oldest. Outcome: Success | Rejected | Failed.
public class AuditLogListExtras
{
    public string? Keyword { get; set; }
    public int? EntityId { get; set; }
    public string? Sort { get; set; }
    public string? Outcome { get; set; }
}
