using ITAM.API.Models.Enums;

namespace ITAM.API.Helpers;

// 5 bước chính mặc định của luồng thanh lý (khớp dữ liệu gốc trong migration). Dùng để "Khôi phục bước mặc định"
// khi Admin IT đã xoá bớt bước chính.
public static class DisposalStatusDefaults
{
    public sealed record Definition(string Code, string Name, string Description, string Color, int SortOrder);

    public static readonly IReadOnlyList<Definition> All = new[]
    {
        new Definition(DisposalStatusCodes.Inspected, "Đã kiểm tra", "Technician đã kiểm tra tài sản", "info", 1),
        new Definition(DisposalStatusCodes.Proposed, "Đã đề xuất", "Technician đề xuất thanh lý, chờ Manager duyệt", "warning", 2),
        new Definition(DisposalStatusCodes.Approved, "Đã duyệt", "Manager đã duyệt, chờ Admin IT thực hiện thanh lý", "success", 3),
        new Definition(DisposalStatusCodes.Rejected, "Từ chối", "Manager từ chối đề xuất", "danger", 4),
        new Definition(DisposalStatusCodes.Completed, "Hoàn tất", "Admin IT đã thanh lý, tài sản chuyển sang Đã thanh lý", "slate", 5),
    };
}
