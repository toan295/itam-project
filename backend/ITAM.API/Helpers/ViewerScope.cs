using ITAM.API.Authorization;
using ITAM.API.Services.Implementations;

namespace ITAM.API.Helpers;

// Phạm vi dữ liệu của người xem (Dashboard, báo cáo xuất file): Admin IT toàn hệ thống và tuỳ chọn lọc phòng ban; mọi vai trò khác
// LUÔN bị khoá ở phòng ban trong token. Không tin tham số từ client: xin phòng ban khác -> DepartmentForbiddenException (403).
public static class ViewerScope
{
    public static (int? DepartmentId, bool Locked) Resolve(string? role, int? userDepartmentId, int? requestedDepartmentId)
    {
        if (role == PermissionCatalog.AdminRole)
        {
            return (requestedDepartmentId, false);
        }

        if (userDepartmentId is not { } own)
        {
            throw new DepartmentForbiddenException();
        }

        if (requestedDepartmentId.HasValue && requestedDepartmentId.Value != own)
        {
            throw new DepartmentForbiddenException();
        }

        return (own, true);
    }
}
