using ITAM.API.Repositories.Models;
using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AuditLogRepository : IAuditLogRepository
{
    public static readonly string[] ImmutabilityTriggerNames = { "trg_AuditLogs_no_update", "trg_AuditLogs_no_delete" };

    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<AuditLog> Items, int TotalItems)> GetPagedAsync(
        int page,
        int pageSize,
        int? userId,
        string? entityName,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtcExclusive,
        AuditLogListExtras extras)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(log => log.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntityName = entityName.Trim();
            query = query.Where(log => log.EntityName == normalizedEntityName);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(log => log.Action == normalizedAction);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.Timestamp >= fromUtc.Value);
        }

        if (toUtcExclusive.HasValue)
        {
            query = query.Where(log => log.Timestamp < toUtcExclusive.Value);
        }

        if (extras.EntityId.HasValue)
        {
            query = query.Where(log => log.EntityId == extras.EntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(extras.Outcome))
        {
            var outcome = extras.Outcome;
            query = query.Where(log => log.Outcome == outcome);
        }

        if (!string.IsNullOrWhiteSpace(extras.Keyword))
        {
            var k = extras.Keyword.Trim();
            query = query.Where(log => log.Action.Contains(k) || log.EntityName.Contains(k)
                || (log.Message != null && log.Message.Contains(k))
                || (log.RequestId != null && log.RequestId.Contains(k))
                || (log.Detail != null && log.Detail.Contains(k))
                || (log.OldValue != null && log.OldValue.Contains(k)) || (log.NewValue != null && log.NewValue.Contains(k))
                || (log.ActorName != null && log.ActorName.Contains(k))
                || log.User.FullName.Contains(k) || (!log.User.IsDeleted && log.User.Email.Contains(k)));
        }

        var totalItems = await query.CountAsync();
        var ordered = extras.Sort == "oldest"
            ? query.OrderBy(log => log.Timestamp).ThenBy(log => log.Id)
            : query.OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.Id);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }

    public Task<AuditLog?> GetByIdWithDetailsAsync(long id) =>
        _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .FirstOrDefaultAsync(log => log.Id == id);

    public void Add(AuditLog auditLog) => _context.AuditLogs.Add(auditLog);

    public Task<AuditActorSnapshot?> GetActorSnapshotAsync(int userId) =>
        _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AuditActorSnapshot(u.FullName, u.Role.Name))
            .FirstOrDefaultAsync();

    public async Task<List<AuditActorRow>> GetActorsAsync()
    {
        var counts = await _context.AuditLogs.AsNoTracking()
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        var ids = counts.Select(c => c.UserId).ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsDeleted, Role = u.Role.Name })
            .ToListAsync();
        var countById = counts.ToDictionary(c => c.UserId, c => c.Count);
        return users
            .Select(u => new AuditActorRow(u.Id, u.FullName, u.Email, u.IsDeleted, u.Role, countById[u.Id]))
            .OrderBy(u => u.Name, StringComparer.Ordinal)
            .ToList();
    }

    // column: tên cột ĐÃ ĐƯỢC KIỂM TRA ở nơi gọi (hằng), không bao giờ là đầu vào người dùng.
    public async Task<Dictionary<string, int>> CountByAsync(string column)
    {
        var query = _context.AuditLogs.AsNoTracking();
        var rows = column switch
        {
            "EntityName" => await query.GroupBy(l => l.EntityName).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(),
            "Action" => await query.GroupBy(l => l.Action).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(),
            "Outcome" => await query.GroupBy(l => l.Outcome).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
        return rows.ToDictionary(r => r.Key, r => r.Count);
    }

    public Task<List<AuditLog>> GetBatchAsync(long afterId, int take) =>
        _context.AuditLogs.AsNoTracking().Where(l => l.Id > afterId).OrderBy(l => l.Id).Take(take).ToListAsync();

    public async Task<bool> ImmutabilityTriggersPresentAsync()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            var wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) await connection.OpenAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE() AND EVENT_OBJECT_TABLE = 'AuditLogs' " +
                    "AND TRIGGER_NAME IN ('trg_AuditLogs_no_update','trg_AuditLogs_no_delete')";
                return Convert.ToInt32(await command.ExecuteScalarAsync()) == ImmutabilityTriggerNames.Length;
            }
            finally
            {
                if (wasClosed) await connection.CloseAsync();
            }
        }
        catch
        {
            return false;
        }
    }
}
