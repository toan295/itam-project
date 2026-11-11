using System.Security.Claims;
using ITAM.API.Filters;
using ITAM.API.Models.Entities;

namespace ITAM.API.Auditing;

/// <summary>
/// Ghi nhật ký cho yêu cầu bị chặn TRƯỚC khi vào controller (không đủ quyền, tài khoản bị giới hạn, hệ thống kiểm tra quyền lỗi),
/// nơi bộ lọc hành động không bao giờ chạy. Chỉ ghi thao tác GHI của người đã đăng nhập (không có danh tính thì không thể gán cho ai).
/// </summary>
public static class AuditDenied
{
    public static async Task RecordAsync(HttpContext http, int status, string message, string outcome = AuditOutcome.Rejected)
    {
        if (!int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || http.Request.RouteValues["controller"] is not string controller)
        {
            return;
        }

        var segments = AuditActionFilter.GetSegmentsAfterResource(http.Request.Path.Value, controller);
        var target = AuditTargets.Resolve(controller, http.Request.Method, segments);
        if (target is null)
        {
            return;
        }

        var entityId = target.EntityIdRoute is not null && int.TryParse(http.Request.RouteValues[target.EntityIdRoute]?.ToString(), out var id)
            ? id
            : target.Entity == "User" && target.EntityIdRoute is null ? userId : 0;
        var detail = http.Request.RouteValues
            .Where(r => r.Key is not ("controller" or "action") && r.Value is not null)
            .ToDictionary(r => r.Key, r => (object?)r.Value!.ToString());

        await http.RequestServices.GetRequiredService<AuditEventWriter>().WriteAsync(new AuditEvent(
            userId, target.Action, target.Entity, entityId, outcome, null, null, status, message, detail.Count == 0 ? null : detail));
    }
}
