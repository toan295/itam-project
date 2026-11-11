namespace ITAM.API.Models.DTOs.AssetAllocations;

public class OverdueAllocationDto
{
    public int AllocationId { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public DateOnly? LastMaintenanceDate { get; set; }
    public int DaysSinceLastMaintenance { get; set; }

    // Mốc dùng để tính: lần bảo trì HOÀN TẤT gần nhất, nếu chưa có thì ngày mua, nếu cũng không có thì ngày giao.
    public DateOnly ReferenceDate { get; set; }
    public string ReferenceSource { get; set; } = "LastMaintenance"; // LastMaintenance | PurchaseDate | AllocatedDate
    public DateOnly AllocatedDate { get; set; }
    public int ThresholdDays { get; set; }

    // High: quá hạn gấp đôi ngưỡng trở lên; Normal: còn lại.
    public string Severity { get; set; } = "Normal";
}
