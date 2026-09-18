using ITAM.API.Models.DTOs.Roles;

namespace ITAM.API.Services.Interfaces;

public interface IRoleService
{
    Task<List<RoleResponseDto>> GetAllAsync();
}
