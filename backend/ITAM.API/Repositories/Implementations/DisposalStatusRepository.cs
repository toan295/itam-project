using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class DisposalStatusRepository : IDisposalStatusRepository
{
    private readonly AppDbContext _db;

    public DisposalStatusRepository(AppDbContext db)
    {
        _db = db;
    }

    // Trạng thái hệ thống (bước chính) lên trước, trong mỗi nhóm theo SortOrder rồi Id — ổn định giữa các lần gọi.
    public Task<List<DisposalStatus>> GetAllAsync() =>
        _db.DisposalStatuses
            .OrderByDescending(s => s.IsSystem).ThenBy(s => s.SortOrder).ThenBy(s => s.Id)
            .ToListAsync();

    public Task<DisposalStatus?> GetByIdAsync(int id) =>
        _db.DisposalStatuses.FirstOrDefaultAsync(s => s.Id == id);

    public Task<DisposalStatus?> GetByCodeAsync(string code) =>
        _db.DisposalStatuses.FirstOrDefaultAsync(s => s.Code == code);

    public Task<bool> NameExistsAsync(string name, int? excludeId = null) =>
        _db.DisposalStatuses.AnyAsync(s => s.Name == name && (excludeId == null || s.Id != excludeId));

    public Task<bool> IsUsedAsSubStatusAsync(int id) =>
        _db.DisposalRequests.AnyAsync(r => r.SubStatusId == id);

    public Task<bool> IsInUseAsync(int id) =>
        _db.DisposalRequests.AnyAsync(r => r.StatusId == id || r.SubStatusId == id);

    public async Task AddAsync(DisposalStatus status) => await _db.DisposalStatuses.AddAsync(status);

    public void Remove(DisposalStatus status) => _db.DisposalStatuses.Remove(status);

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
