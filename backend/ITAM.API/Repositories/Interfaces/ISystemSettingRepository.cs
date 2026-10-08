using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key);
    Task AddAsync(SystemSetting setting);
    void Update(SystemSetting setting);
    void Remove(SystemSetting setting);
    Task<int> SaveChangesAsync();
}
