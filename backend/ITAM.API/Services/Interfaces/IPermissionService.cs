using ITAM.API.Models.DTOs.Permissions;

namespace ITAM.API.Services.Interfaces;

public interface IPermissionService
{
    Task<PermissionMatrixDto> GetMatrixAsync();

    /// <exception cref="KeyNotFoundException">Vai trò không tồn tại.</exception>
    /// <exception cref="InvalidOperationException">Vai trò bị khoá (Nhân viên).</exception>
    /// <exception cref="ArgumentException">Mã quyền không hợp lệ, hoặc vi phạm quy tắc quyền hệ thống.</exception>
    Task<RolePermissionsDto> UpdateRoleAsync(int roleId, UpdateRolePermissionsRequestDto dto, int actorUserId);

    /// <exception cref="InvalidOperationException">Revision lệch (người khác vừa sửa) hoặc vai trò bị khoá.</exception>
    Task<RolePermissionsDto> ResetRoleAsync(int roleId, ResetRolePermissionsRequestDto? dto, int actorUserId);

    /// <summary>Lưu nhiều vai trò trong một transaction (tất cả hoặc không gì cả) và trả lại ma trận mới nhất do server xác nhận.</summary>
    Task<PermissionMatrixDto> UpdateMatrixAsync(UpdatePermissionMatrixRequestDto dto, int actorUserId);

    /// <summary>Danh sách mã quyền của một vai trò — gửi kèm khi đăng nhập để giao diện ẩn/hiện nút.</summary>
    Task<List<string>> GetPermissionsForRoleAsync(string roleName);
}
