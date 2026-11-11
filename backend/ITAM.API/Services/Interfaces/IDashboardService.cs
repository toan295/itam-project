using ITAM.API.Models.DTOs.Dashboard;

namespace ITAM.API.Services.Interfaces;

// Người xem: vai trò, phòng ban trong token và tập quyền (quyết định widget nào hiển thị). Phạm vi do SERVICE ép, không tin tham số client.
public sealed record DashboardQuery(
    string? Role,
    int? UserDepartmentId,
    int? RequestedDepartmentId,
    int? Year,
    IReadOnlySet<string> Permissions,
    bool Refresh = false);

public interface IDashboardService
{
    // Admin IT: toàn hệ thống hoặc lọc phòng ban. Vai trò khác: LUÔN bị khoá ở phòng ban của mình; xin phòng ban khác -> DepartmentForbiddenException.
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query);
}
