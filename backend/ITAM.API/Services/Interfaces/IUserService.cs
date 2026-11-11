using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Services.Interfaces;

public interface IUserService
{
    Task<PagedResultDto<UserResponseDto>> GetPagedAsync(int page, int pageSize, UserListFilter filter);
    Task<UserResponseDto> GetByIdAsync(int id);
    Task<UserResponseDto> CreateAsync(CreateUserRequestDto dto, int currentUserId);
    Task<UserResponseDto> UpdateAsync(int id, UpdateUserRequestDto dto, int currentUserId);
    Task<UserResponseDto> UpdateOwnProfileAsync(int userId, string? fullName, string? phone);
    Task<UserResponseDto> ResetPasswordAsync(int id, int currentUserId);

    // Xoá tài khoản (xoá mềm): vô hiệu đăng nhập, giải phóng email, tách khỏi hồ sơ nhân viên; giữ lịch sử.
    Task DeleteAsync(int id, int currentUserId);

    // Gửi lại email kích hoạt cho tài khoản đang chờ kích hoạt.
    Task<UserResponseDto> ResendActivationAsync(int id, int currentUserId);

    // Nhân viên đang hoạt động chưa có tài khoản — để chọn khi tạo tài khoản.
    Task<List<EmployeeCandidateDto>> GetEmployeeCandidatesAsync(string? keyword);
}
