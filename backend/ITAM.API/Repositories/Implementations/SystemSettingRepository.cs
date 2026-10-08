using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly AppDbContext _db;

    public SystemSettingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<SystemSetting?> GetByKeyAsync(string key) =>
        _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);

    public async Task AddAsync(SystemSetting setting) => await _db.SystemSettings.AddAsync(setting);

    public void Update(SystemSetting setting) => _db.SystemSettings.Update(setting);

    public void Remove(SystemSetting setting) => _db.SystemSettings.Remove(setting);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
