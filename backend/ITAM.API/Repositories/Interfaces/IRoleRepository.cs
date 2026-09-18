using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IRoleRepository
{
    Task<List<Role>> GetAllAsync();
    Task<bool> ExistsAsync(int roleId);
}
