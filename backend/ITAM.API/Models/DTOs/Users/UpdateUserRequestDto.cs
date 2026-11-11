namespace ITAM.API.Models.DTOs.Users;

// Admin IT sửa thông tin của mọi tài khoản: họ tên, email, SĐT, phòng ban (tuỳ chọn — bỏ trống = giữ nguyên), vai trò và khoá/mở khoá.
// Tài khoản gắn hồ sơ nhân viên: hồ sơ được cập nhật cùng lúc (một lần lưu) nên hai nơi không bao giờ lệch nhau.
public class UpdateUserRequestDto
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    // Chuỗi rỗng = xoá số điện thoại.
    public string? Phone { get; set; }
    public int? DepartmentId { get; set; }

    public int RoleId { get; set; }

    // Gộp "khoá tài khoản" vào Update — true = đang hoạt động, false = đã khoá. UserService chặn tự khoá chính mình
    // và khoá/hạ quyền Admin IT cuối cùng.
    public bool IsActive { get; set; }
}
