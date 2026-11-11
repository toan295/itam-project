using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class RolePermissionRepository : IRolePermissionRepository
{
    private readonly AppDbContext _db;

    public RolePermissionRepository(AppDbContext db) => _db = db;

    public async Task<Dictionary<string, HashSet<string>>> GetAllByRoleNameAsync()
    {
        var roles = await _db.Roles.AsNoTracking().Select(r => r.Name).ToListAsync();
        var rows = await _db.RolePermissions.AsNoTracking()
            .Select(rp => new { RoleName = rp.Role.Name, rp.PermissionCode })
            .ToListAsync();

        var result = roles.ToDictionary(name => name, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach (var row in rows)
        {
            result[row.RoleName].Add(row.PermissionCode);
        }

        return result;
    }

    public async Task<Dictionary<int, List<string>>> GetAllByRoleIdAsync()
    {
        var rows = await _db.RolePermissions.AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.PermissionCode })
            .ToListAsync();
        return rows.GroupBy(r => r.RoleId).ToDictionary(g => g.Key, g => g.Select(r => r.PermissionCode).ToList());
    }

    public async Task<Dictionary<int, int>> CountUsersByRoleAsync() =>
        await _db.Users.AsNoTracking().Where(u => !u.IsDeleted).GroupBy(u => u.RoleId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    public Task ReplaceAsync(int roleId, IReadOnlyCollection<string> permissionCodes) =>
        ReplaceManyAsync(new Dictionary<int, IReadOnlyCollection<string>> { [roleId] = permissionCodes });

    public async Task ReplaceManyAsync(IReadOnlyDictionary<int, IReadOnlyCollection<string>> permissionCodesByRole)
    {
        var roleIds = permissionCodesByRole.Keys.ToList();
        var existing = await _db.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)).ToListAsync();

        // Chỉ xoá/thêm phần chênh lệch (không xoá rồi thêm lại cùng khoá chính).
        foreach (var (roleId, codes) in permissionCodesByRole)
        {
            var wanted = codes.ToHashSet(StringComparer.Ordinal);
            var current = existing.Where(rp => rp.RoleId == roleId).ToList();
            _db.RolePermissions.RemoveRange(current.Where(rp => !wanted.Contains(rp.PermissionCode)));
            var have = current.Select(rp => rp.PermissionCode).ToHashSet(StringComparer.Ordinal);
            _db.RolePermissions.AddRange(wanted.Where(code => !have.Contains(code))
                .Select(code => new RolePermission { RoleId = roleId, PermissionCode = code }));
        }

        await _db.SaveChangesAsync(); // một SaveChanges = một transaction: không để vai trò ở trạng thái "nửa quyền".
    }
}
