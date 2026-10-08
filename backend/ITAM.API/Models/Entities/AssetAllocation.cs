using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

public class AssetAllocation
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int DepartmentId { get; set; }
    // Bản chụp họ tên người nhận lúc phân bổ (giữ nguyên trên biên bản dù nhân viên sau này đổi tên); nguồn thật là Employee.
    public string RecipientName { get; set; } = null!;
    public int? EmployeeId { get; set; }
    public DateOnly AllocatedDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public string? HandoverNote { get; set; }
    public string? HandoverReason { get; set; }       // "Vì lý do ..." trên biên bản.
    public string? HandoverLocation { get; set; }     // "tại ..." trên biên bản.
    public string HandoverCondition { get; set; } = "Tốt"; // Tình trạng tài sản lúc bàn giao.
    public int? HandedOverByUserId { get; set; }      // Bên giao: người dùng thực hiện phân bổ.
    public int? ReceivedByUserId { get; set; }        // Bên nhận lại khi thu hồi: người dùng thực hiện thu hồi.
    public AssetReturnCondition? ReturnCondition { get; set; }
    public string? ReturnNote { get; set; }
    public AllocationStatus Status { get; set; }

    public Asset Asset { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Employee? Employee { get; set; }
    public User? HandedOverBy { get; set; }
    public User? ReceivedBy { get; set; }
}
