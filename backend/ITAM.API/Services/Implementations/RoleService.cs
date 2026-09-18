using ITAM.API.Models.DTOs.Roles;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repo;

    public RoleService(IRoleRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<RoleResponseDto>> GetAllAsync()
    {
        var roles = await _repo.GetAllAsync();
        return roles.Select(r => new RoleResponseDto { Id = r.Id, Name = r.Name }).ToList();
    }
}
