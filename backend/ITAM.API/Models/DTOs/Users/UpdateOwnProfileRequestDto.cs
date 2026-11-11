namespace ITAM.API.Models.DTOs.Users;

// Người dùng tự sửa hồ sơ của mình: chỉ họ tên và số điện thoại (bỏ trống = giữ nguyên; Phone rỗng = xoá số).
public class UpdateOwnProfileRequestDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}
