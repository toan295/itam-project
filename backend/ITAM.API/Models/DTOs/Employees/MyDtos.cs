namespace ITAM.API.Models.DTOs.Employees;

// Tài sản nhân viên đang được cấp (phân bổ đang mở).
public class MyAssetDto
{
    public int AllocationId { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = default!;
    public string AssetName { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public string? SerialNumber { get; set; }
    public DateOnly AllocatedDate { get; set; }
    public string AssetStatus { get; set; } = default!;   // InUse | Maintenance | Broken ...
    // Đang có phiếu bảo trì chờ xử lý -> không cần (và không thể) báo thêm.
    public bool HasPendingTicket { get; set; }
}

// Yêu cầu bảo trì do chính nhân viên tạo.
public class MyTicketDto
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = default!;
    public string AssetName { get; set; } = default!;
    public string IssueDescription { get; set; } = default!;
    public string Status { get; set; } = default!;      // Pending | Resolved | Failed
    public string Priority { get; set; } = default!;
    public string? TechnicianName { get; set; }
    public DateTime ReportedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Notes { get; set; }                   // Kết quả xử lý do kỹ thuật viên ghi.
}
