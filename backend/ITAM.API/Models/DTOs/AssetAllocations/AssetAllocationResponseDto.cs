namespace ITAM.API.Models.DTOs.AssetAllocations;

public class AssetAllocationResponseDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public int? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeePosition { get; set; }
    public DateOnly AllocatedDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public string? HandoverNote { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverLocation { get; set; }
    public string HandoverCondition { get; set; } = "";
    public string? ReturnCondition { get; set; }
    public string? ReturnNote { get; set; }
    public string Status { get; set; } = "";

    // DepartmentName/DepartmentId = phòng ban GHI NHẬN LÚC GIAO (cố định, theo biên bản). Hai phòng ban dưới đây là
    // dữ liệu HIỆN TẠI và có thể khác: phòng ban quản lý tài sản, phòng ban hiện tại của nhân viên.
    public string AssetDepartmentName { get; set; } = "";
    public string? EmployeeCurrentDepartmentName { get; set; }

    // Số ngày giữ của CHÍNH lần giao này: (ngày thu hồi hoặc hôm nay theo giờ VN) − ngày giao. Giao và thu hồi cùng ngày = 0.
    public int HoldingDays { get; set; }
    public bool IsOpen { get; set; }
}
