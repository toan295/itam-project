using ITAM.API.Authorization;
using ITAM.API.Models.DTOs.Permissions;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly IRolePermissionRepository _repo;
    private readonly IRoleRepository _roleRepo;
    private readonly IAuditLogService _audit;
    private readonly IRolePermissionCache _cache;
    private readonly IExclusiveSection _exclusive;

    public PermissionService(
        IRolePermissionRepository repo, IRoleRepository roleRepo, IAuditLogService audit, IRolePermissionCache cache,
        IExclusiveSection exclusive)
    {
        _exclusive = exclusive;
        _repo = repo;
        _roleRepo = roleRepo;
        _audit = audit;
        _cache = cache;
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync()
    {
        var roles = await _roleRepo.GetAllAsync();
        var granted = await _repo.GetAllByRoleIdAsync();
        var userCounts = await _repo.CountUsersByRoleAsync();

        return new PermissionMatrixDto
        {
            Groups = PermissionCatalog.All
                .GroupBy(p => p.Group)
                .Select(g => new PermissionGroupDto
                {
                    Name = g.Key,
                    Items = g.Select(p => new PermissionItemDto
                    {
                        Code = p.Code, Label = p.Label, Description = p.Description, SystemOnly = p.SystemOnly,
                    }).ToList(),
                })
                .ToList(),
            Roles = roles.Select(r => MapRole(r, granted.GetValueOrDefault(r.Id), userCounts.GetValueOrDefault(r.Id))).ToList(),
        };
    }

    public Task<RolePermissionsDto> UpdateRoleAsync(int roleId, UpdateRolePermissionsRequestDto dto, int actorUserId) =>
        InExclusiveAsync(async () =>
        {
            var roles = await _roleRepo.GetAllAsync();
            var role = FindEditableRole(roles, roleId);
            var requested = NormalizeAndValidate(role, dto.Permissions);
            var before = (await _repo.GetAllByRoleIdAsync()).GetValueOrDefault(role.Id) ?? new List<string>();
            EnsureRevision(role, before, dto.ExpectedRevision);
            return await SaveAsync(role, before, requested, "UpdatePermissions", actorUserId);
        });

    public Task<RolePermissionsDto> ResetRoleAsync(int roleId, ResetRolePermissionsRequestDto? dto, int actorUserId) =>
        InExclusiveAsync(async () =>
        {
            var roles = await _roleRepo.GetAllAsync();
            var role = FindEditableRole(roles, roleId);
            var before = (await _repo.GetAllByRoleIdAsync()).GetValueOrDefault(role.Id) ?? new List<string>();
            EnsureRevision(role, before, dto?.ExpectedRevision);
            return await SaveAsync(role, before, PermissionCatalog.DefaultsFor(role.Name).ToHashSet(StringComparer.Ordinal), "ResetPermissions", actorUserId);
        });

    // Lưu nhiều vai trò trong MỘT transaction: kiểm tra hết trước, rồi ghi một lần. Một vai trò sai/lệch revision thì không vai trò nào được ghi.
    public async Task<PermissionMatrixDto> UpdateMatrixAsync(UpdatePermissionMatrixRequestDto dto, int actorUserId)
    {
        var changes = dto.Roles;
        if (changes is null || changes.Count == 0)
        {
            throw new ArgumentException("Không có vai trò nào để lưu.");
        }

        if (changes.Select(c => c.RoleId).Distinct().Count() != changes.Count)
        {
            throw new ArgumentException("Một vai trò xuất hiện nhiều lần trong yêu cầu.");
        }

        await InExclusiveAsync(async () =>
        {
            var roles = await _roleRepo.GetAllAsync();
            var granted = await _repo.GetAllByRoleIdAsync();

            var plan = new List<(Role Role, List<string> Before, HashSet<string> After)>();
            foreach (var change in changes)
            {
                var role = FindEditableRole(roles, change.RoleId);
                var after = NormalizeAndValidate(role, change.Permissions);
                var before = granted.GetValueOrDefault(role.Id) ?? new List<string>();
                if (string.IsNullOrWhiteSpace(change.ExpectedRevision))
                {
                    throw new ArgumentException($"Thiếu revision của vai trò {role.Name}. Hãy tải lại ma trận rồi lưu lại.");
                }

                EnsureRevision(role, before, change.ExpectedRevision);
                if (!before.ToHashSet(StringComparer.Ordinal).SetEquals(after))
                {
                    plan.Add((role, before, after));
                }
            }

            if (plan.Count == 0)
            {
                return 0;
            }

            foreach (var (role, before, after) in plan)
            {
                await RecordAuditAsync("UpdatePermissions", actorUserId, role, before, after.OrderBy(c => c, StringComparer.Ordinal).ToList());
            }

            await _repo.ReplaceManyAsync(plan.ToDictionary(
                p => p.Role.Id, p => (IReadOnlyCollection<string>)p.After.OrderBy(c => c, StringComparer.Ordinal).ToList()));
            _cache.Invalidate();
            return plan.Count;
        });

        return await GetMatrixAsync();
    }

    public async Task<List<string>> GetPermissionsForRoleAsync(string roleName) =>
        (await _cache.GetAsync(roleName)).OrderBy(c => c, StringComparer.Ordinal).ToList();

    // Mọi thay đổi ma trận đi qua đây, tuần tự theo khoá dòng vai trò Admin IT (cùng khoá với việc hạ/khoá Admin):
    // hai người lưu cùng lúc không đan xen, và kiểm tra revision/quyền hệ thống luôn dựa trên dữ liệu mới nhất.
    private async Task<T> InExclusiveAsync<T>(Func<Task<T>> work)
    {
        var adminId = await _roleRepo.GetIdByNameAsync(PermissionCatalog.AdminRole);
        return adminId is null ? await work() : await _exclusive.RunAsync(LockTarget.Role, adminId.Value, work);
    }

    private static Role FindEditableRole(List<Role> roles, int roleId)
    {
        var role = roles.FirstOrDefault(r => r.Id == roleId)
            ?? throw new KeyNotFoundException($"Không tìm thấy vai trò Id = {roleId}.");

        if (PermissionCatalog.IsLockedRole(role.Name))
        {
            throw new InvalidOperationException("Vai trò Nhân viên chỉ dùng cổng \"Tài sản của tôi\", không chỉnh được quyền.");
        }

        return role;
    }

    private static HashSet<string> NormalizeAndValidate(Role role, List<string>? permissions)
    {
        if (permissions is null)
        {
            throw new ArgumentException("Thiếu danh sách quyền (permissions).");
        }

        var requested = permissions.Select(c => c?.Trim() ?? "").Where(c => c.Length > 0).ToHashSet(StringComparer.Ordinal);

        var unknown = requested.Where(c => !PermissionCatalog.IsKnown(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Mã quyền không hợp lệ: {string.Join(", ", unknown)}.");
        }

        if (role.Name == PermissionCatalog.AdminRole)
        {
            // Admin IT luôn giữ quyền hệ thống — nếu gỡ, không ai còn vào được trang phân công hay quản lý người dùng.
            var missing = PermissionCatalog.SystemCodes.Where(c => !requested.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                throw new ArgumentException($"Admin IT phải luôn giữ các quyền hệ thống: {string.Join(", ", missing)}.");
            }
        }
        else
        {
            var system = requested.Where(PermissionCatalog.IsSystemOnly).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (system.Count > 0)
            {
                throw new ArgumentException($"Quyền hệ thống chỉ dành cho Admin IT: {string.Join(", ", system)}.");
            }
        }

        return requested;
    }

    private static void EnsureRevision(Role role, List<string> current, string? expectedRevision)
    {
        if (!string.IsNullOrWhiteSpace(expectedRevision)
            && !string.Equals(expectedRevision.Trim(), PermissionRevision.Compute(current), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Quyền của vai trò {role.Name} vừa được người khác thay đổi. Hãy tải lại ma trận để xem bản mới nhất rồi chỉnh lại.");
        }
    }

    private async Task<RolePermissionsDto> SaveAsync(Role role, List<string> before, HashSet<string> codes, string action, int actorUserId)
    {
        var ordered = codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
        await RecordAuditAsync(action, actorUserId, role, before, ordered);

        // RecordAsync chỉ thêm vào DbContext dùng chung; một SaveChanges trong ReplaceAsync ghi cả quyền lẫn nhật ký (atomic).
        await _repo.ReplaceAsync(role.Id, ordered);
        _cache.Invalidate();

        var userCounts = await _repo.CountUsersByRoleAsync();
        return MapRole(role, ordered, userCounts.GetValueOrDefault(role.Id));
    }

    private Task RecordAuditAsync(string action, int actorUserId, Role role, List<string> before, List<string> after)
    {
        var added = after.Except(before, StringComparer.Ordinal).ToList();
        var removed = before.Except(after, StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return _audit.RecordAsync(actorUserId, action, "Role", role.Id,
            oldValue: new { role = role.Name, permissions = before.OrderBy(c => c, StringComparer.Ordinal).ToList() },
            newValue: new { role = role.Name, added, removed });
    }

    private static RolePermissionsDto MapRole(Role role, List<string>? granted, int userCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        UserCount = userCount,
        IsLocked = PermissionCatalog.IsLockedRole(role.Name),
        Permissions = (granted ?? new List<string>()).OrderBy(c => c, StringComparer.Ordinal).ToList(),
        DefaultPermissions = PermissionCatalog.DefaultsFor(role.Name).OrderBy(c => c, StringComparer.Ordinal).ToList(),
        Revision = PermissionRevision.Compute(granted ?? new List<string>()),
    };
}
