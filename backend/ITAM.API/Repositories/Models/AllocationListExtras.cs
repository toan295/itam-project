using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Models;

// Bộ lọc bổ sung cho danh sách phân bổ (Service đã validate).
// HoldingBefore: chỉ phân bổ ĐANG MỞ có ngày giao TRƯỚC ngày này (= số ngày giữ lớn hơn N, "đang giữ quá N ngày").
// Sort: newest (mặc định, theo ngày giao) | oldest.
public class AllocationListExtras
{
    public int? CategoryId { get; set; }
    public DateOnly? AllocatedFrom { get; set; }
    public DateOnly? AllocatedTo { get; set; }
    public string? HandoverCondition { get; set; }
    public AssetReturnCondition? ReturnCondition { get; set; }
    public DateOnly? HoldingBefore { get; set; }
    public string? Sort { get; set; }

    // Ý nghĩa của bộ lọc phòng ban: "handover" (mặc định) = phòng ban ghi nhận lúc giao; "employee" = phòng ban HIỆN TẠI của
    // nhân viên; "asset" = phòng ban HIỆN TẠI đang quản lý tài sản.
    public string DepartmentScope { get; set; } = "handover";
}
