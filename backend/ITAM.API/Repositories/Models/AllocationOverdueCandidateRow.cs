namespace ITAM.API.Repositories.Models;

// Dòng dữ liệu rút gọn phục vụ cảnh báo quá hạn bảo trì (mục "Phân bổ" — Mục 2.2 file tham chiếu #1) —
// chỉ các cột cần để Service tự tính "quá hạn hay chưa", không phải quyết định nghiệp vụ.
public class AllocationOverdueCandidateRow
{
    public int AllocationId { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public DateOnly AllocatedDate { get; set; }
    public DateOnly? AssetPurchaseDate { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
}
