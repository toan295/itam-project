using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Services.Interfaces;

public interface IUserService
{
    Task<PagedResultDto<UserResponseDto>> GetPagedAsync(int page, int pageSize, string? search);
    Task<UserResponseDto> GetByIdAsync(int id);
    Task<UserResponseDto> CreateAsync(CreateUserRequestDto dto, int currentUserId);
    Task<UserResponseDto> UpdateAsync(int id, UpdateUserRequestDto dto, int currentUserId);
    Task<UserResponseDto> ResetPasswordAsync(int id, int currentUserId);
}
