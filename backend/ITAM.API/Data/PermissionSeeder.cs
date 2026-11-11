using ITAM.API.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Data;

/// <summary>
/// Khởi tạo ma trận quyền mặc định (chạy ở mọi môi trường, sau khi các vai trò đã tồn tại). Idempotent:
/// - Lần đầu (Admin IT chưa có dòng quyền nào): ghi quyền mặc định cho mọi vai trò — giữ nguyên hành vi trước khi có giao diện phân quyền.
/// - Các lần sau: không đụng vào cấu hình đã chỉnh, chỉ bảo đảm Admin IT còn đủ quyền hệ thống.
/// </summary>
public static class PermissionSeeder
{
    public static async Task EnsureAsync(AppDbContext db, ILogger logger)
    {
        var roles = await db.Roles.ToListAsync();
        var admin = roles.FirstOrDefault(r => r.Name == PermissionCatalog.AdminRole);
        if (admin is null)
        {
            return; // Chưa có vai trò nào (DB mới, chưa bootstrap) — lần khởi động sau sẽ seed.
        }

        // Dọn mã quyền đã bị gỡ khỏi danh mục (vd "employees.issue-account") để ma trận không còn dòng mồ côi.
        var obsolete = await db.RolePermissions.ToListAsync();
        obsolete = obsolete.Where(rp => !PermissionCatalog.IsKnown(rp.PermissionCode)).ToList();
        if (obsolete.Count > 0)
        {
            db.RolePermissions.RemoveRange(obsolete);
            await db.SaveChangesAsync();
            logger.LogInformation("Đã xoá {Count} dòng quyền không còn trong danh mục.", obsolete.Count);
        }

        var adminCodes = await db.RolePermissions.Where(rp => rp.RoleId == admin.Id).Select(rp => rp.PermissionCode).ToListAsync();

        if (adminCodes.Count == 0)
        {
            foreach (var role in roles)
            {
                foreach (var code in PermissionCatalog.DefaultsFor(role.Name))
                {
                    db.RolePermissions.Add(new Models.Entities.RolePermission { RoleId = role.Id, PermissionCode = code });
                }
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Đã khởi tạo ma trận phân quyền mặc định cho {Count} vai trò.", roles.Count);
            return;
        }

        var missing = PermissionCatalog.SystemCodes.Except(adminCodes, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            db.RolePermissions.AddRange(missing.Select(code => new Models.Entities.RolePermission { RoleId = admin.Id, PermissionCode = code }));
            await db.SaveChangesAsync();
            logger.LogWarning("Đã bổ sung quyền hệ thống còn thiếu cho Admin IT: {Codes}", string.Join(", ", missing));
        }
    }
}
