namespace ITAM.API.Models.Entities;

// Danh mục trạng thái của phiếu thanh lý, Admin IT quản lý được (thêm/sửa/xoá).
//  - IsSystem = true: 5 bước chính của luồng (xem DisposalStatusCodes) — chỉ sửa Name/Description/Color/SortOrder.
//  - IsSystem = false: trạng thái phụ do Admin thêm — Admin IT gán thủ công vào phiếu (SubStatusId), không đổi luồng.
public class DisposalStatus
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = "slate";
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }

    // Xoá mềm: bấm "Xoá" trên giao diện chỉ đánh dấu dòng này, dữ liệu vẫn còn trong CSDL (và không hiện lại khi chạy lại hệ thống).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
