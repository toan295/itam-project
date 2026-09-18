namespace ITAM.API.Models.DTOs.Users;

public class UpdateUserRequestDto
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int RoleId { get; set; }
    public int DepartmentId { get; set; }

    // Gộp "khoá tài khoản" vào Update (giống UpdateAssetRequestDto gộp Status) — true = đang hoạt
    // động, false = đã khoá. UserService chặn tự khoá chính mình và khoá/hạ quyền Admin IT cuối cùng.
    public bool IsActive { get; set; }
}
