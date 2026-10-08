namespace ITAM.API.Models.DTOs.AssetAllocations;

public class CreateAssetAllocationDto
{
    public int AssetId { get; set; }

    // Người nhận phải CHỌN từ danh mục nhân viên (/employees) — không nhập tên tự do. Phòng ban nhận lấy theo nhân viên.
    public int EmployeeId { get; set; }

    public DateOnly AllocatedDate { get; set; }
    public string? HandoverReason { get; set; }      // "Vì lý do ..." trên biên bản.
    public string? HandoverLocation { get; set; }    // "tại ..." trên biên bản.
    public string HandoverCondition { get; set; } = "Tốt"; // Mới | Tốt | Bình thường | Cũ
    public string? HandoverNote { get; set; }
}
