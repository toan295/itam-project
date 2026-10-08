using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IDisposalStatusRepository
{
    Task<List<DisposalStatus>> GetAllAsync();
    Task<DisposalStatus?> GetByIdAsync(int id);
    Task<DisposalStatus?> GetByCodeAsync(string code);
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
    Task<bool> IsUsedAsSubStatusAsync(int id);
    Task<bool> IsInUseAsync(int id); // đang là bước hiện tại HOẶC trạng thái phụ của ít nhất một phiếu.
    Task AddAsync(DisposalStatus status);
    void Remove(DisposalStatus status);
    Task<int> SaveChangesAsync();
}
