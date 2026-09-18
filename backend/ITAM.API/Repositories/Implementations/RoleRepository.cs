using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _db;

    public RoleRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<Role>> GetAllAsync() =>
        _db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();

    public Task<bool> ExistsAsync(int roleId) =>
        _db.Roles.AnyAsync(r => r.Id == roleId);
}
