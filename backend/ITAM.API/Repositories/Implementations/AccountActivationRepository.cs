using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AccountActivationRepository : IAccountActivationRepository
{
    private readonly AppDbContext _db;

    public AccountActivationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AccountActivation activation) => await _db.AccountActivations.AddAsync(activation);

    public Task<AccountActivation?> GetByTokenHashAsync(string tokenHash) =>
        _db.AccountActivations
            .AsNoTracking()
            .Include(a => a.User).ThenInclude(u => u.Role)
            .Include(a => a.User).ThenInclude(u => u.Department)
            .FirstOrDefaultAsync(a => a.TokenHash == tokenHash);

    public Task<List<AccountActivation>> GetPendingByUserAsync(int userId) =>
        _db.AccountActivations.Where(a => a.UserId == userId && a.Status == ActivationStatus.Pending).ToListAsync();

    public async Task<Dictionary<int, AccountActivation>> GetLatestByUsersAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, AccountActivation>();
        }

        var rows = await _db.AccountActivations.AsNoTracking()
            .Where(a => ids.Contains(a.UserId))
            .OrderByDescending(a => a.Id)
            .ToListAsync();
        return rows.GroupBy(a => a.UserId).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<bool> TryActivateAsync(int activationId, int userId, string passwordHash, DateTime now)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var claimed = await _db.AccountActivations
            .Where(a => a.Id == activationId && a.Status == ActivationStatus.Pending && a.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, ActivationStatus.Activated)
                .SetProperty(a => a.ActivatedAt, now));
        if (claimed == 0)
        {
            return false;
        }

        var updated = await _db.Users
            .Where(u => u.Id == userId && u.PendingActivation)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordHash, passwordHash)
                .SetProperty(u => u.PendingActivation, false)
                .SetProperty(u => u.MustChangePassword, false));
        if (updated == 0)
        {
            return false; // tài khoản đã được kích hoạt/không còn chờ kích hoạt -> huỷ transaction.
        }

        await tx.CommitAsync();
        return true;
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
}
