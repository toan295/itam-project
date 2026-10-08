using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class AuditLogRepository : IAuditLogRepository
{
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
        DateTime? fromDate,
        DateTime? toDate)
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

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(log => log.Timestamp >= from);
        }

        if (toDate.HasValue)
        {
            // Giá trị từ input type=date là 00:00 của ngày được chọn; dùng mốc đầu ngày kế
            // tiếp để bao gồm toàn bộ bản ghi của ngày toDate.
            var toExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(log => log.Timestamp < toExclusive);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(log => log.Timestamp)
            .ThenByDescending(log => log.Id)
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
}
