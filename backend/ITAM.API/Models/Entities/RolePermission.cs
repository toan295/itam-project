namespace ITAM.API.Models.Entities;

/// <summary>Một quyền được cấp cho một vai trò (khoá chính ghép RoleId + PermissionCode). Danh mục mã quyền nằm ở code (PermissionCatalog).</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string PermissionCode { get; set; } = null!;
}
