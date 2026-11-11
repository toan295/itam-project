namespace ITAM.API.Repositories.Interfaces;

public interface IRolePermissionRepository
{
    /// <summary>Tên vai trò → tập mã quyền (mọi vai trò, gồm vai trò chưa có quyền nào).</summary>
    Task<Dictionary<string, HashSet<string>>> GetAllByRoleNameAsync();

    /// <summary>Mã vai trò → danh sách mã quyền.</summary>
    Task<Dictionary<int, List<string>>> GetAllByRoleIdAsync();

    /// <summary>Số người dùng đang giữ từng vai trò.</summary>
    Task<Dictionary<int, int>> CountUsersByRoleAsync();

    /// <summary>Thay toàn bộ quyền của vai trò bằng tập mới trong một lần lưu.</summary>
    Task ReplaceAsync(int roleId, IReadOnlyCollection<string> permissionCodes);

    /// <summary>Thay quyền của nhiều vai trò trong CÙNG một lần lưu (một transaction): hoặc tất cả cùng đổi, hoặc không vai trò nào đổi.</summary>
    Task ReplaceManyAsync(IReadOnlyDictionary<int, IReadOnlyCollection<string>> permissionCodesByRole);
}
