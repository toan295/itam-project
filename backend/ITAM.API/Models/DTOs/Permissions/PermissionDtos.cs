namespace ITAM.API.Models.DTOs.Permissions;

public class PermissionItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Quyền hệ thống: chỉ Admin IT, luôn bật.</summary>
    public bool SystemOnly { get; set; }
}

public class PermissionGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<PermissionItemDto> Items { get; set; } = new();
}

public class RolePermissionsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    /// <summary>Vai trò bị khoá (Nhân viên): xem được, không sửa được.</summary>
    public List<string> Permissions { get; set; } = new();
    public List<string> DefaultPermissions { get; set; } = new();
    /// <summary>Dấu phiên bản của tập quyền hiện tại — gửi lại làm ExpectedRevision khi lưu để phát hiện sửa đồng thời.</summary>
    public string Revision { get; set; } = string.Empty;
}

public class PermissionMatrixDto
{
    public List<PermissionGroupDto> Groups { get; set; } = new();
    public List<RolePermissionsDto> Roles { get; set; } = new();
}

public class UpdateRolePermissionsRequestDto
{
    public List<string> Permissions { get; set; } = new();
    /// <summary>Tuỳ chọn: revision đã thấy lúc tải ma trận; lệch với hiện tại thì 409.</summary>
    public string? ExpectedRevision { get; set; }
}

public class ResetRolePermissionsRequestDto
{
    public string? ExpectedRevision { get; set; }
}

public class RolePermissionChangeDto
{
    public int RoleId { get; set; }
    public List<string>? Permissions { get; set; }
    /// <summary>Bắt buộc: revision của vai trò lúc người dùng mở ma trận.</summary>
    public string? ExpectedRevision { get; set; }
}

/// <summary>Lưu nhiều vai trò trong một lần: tất cả cùng thành công hoặc không có gì thay đổi.</summary>
public class UpdatePermissionMatrixRequestDto
{
    public List<RolePermissionChangeDto> Roles { get; set; } = new();
}
