using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Repositories.Implementations;

public class MaintenanceTicketRepository : IMaintenanceTicketRepository
{
    private const string TechnicianRoleName = "Technician";

    private readonly AppDbContext _db;

    public MaintenanceTicketRepository(AppDbContext db)
    {
        _db = db;
    }

    // Cố ý KHÔNG dùng AsNoTracking (khác AssetRepository): UpdateStatusAsync sửa cả ticket lẫn ticket.Asset
    // rồi lưu bằng đúng 1 lần SaveChangesAsync — cần cả 2 entity cùng được track bởi 1 AppDbContext để
    // ghi trong cùng 1 transaction ngầm của EF Core.
    public Task<MaintenanceTicket?> GetByIdWithDetailsAsync(int id) =>
        _db.MaintenanceTickets
            .Include(t => t.Asset)
            .Include(t => t.Technician)
            .Include(t => t.ReportedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task AddAsync(MaintenanceTicket ticket) => await _db.MaintenanceTickets.AddAsync(ticket);

    // Ticket đọc từ GetByIdWithDetailsAsync đã được track, change-tracking chỉ ghi đúng các cột thực sự đổi
    // (tránh ghi đè cả dòng, vd đóng phiếu ghi đè TechnicianId vừa được gán lại bởi request khác).
    // Chỉ khi entity bị detached mới ép Modified — và vẫn không dùng _db.MaintenanceTickets.Update() vì nó
    // lan sang cả Asset/Technician.
    public void Update(MaintenanceTicket ticket)
    {
        var entry = _db.Entry(ticket);
        if (entry.State == EntityState.Detached)
        {
            entry.State = EntityState.Modified;
        }
    }

    public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<bool> HasPendingTicketAsync(int assetId) =>
        _db.MaintenanceTickets.AnyAsync(t => t.AssetId == assetId && t.Status == TicketStatus.Pending);

    public Task<bool> HasOtherPendingTicketsAsync(int assetId, int excludeTicketId) =>
        _db.MaintenanceTickets.AnyAsync(t =>
            t.AssetId == assetId && t.Status == TicketStatus.Pending && t.Id != excludeTicketId);

    public Task<List<MaintenanceTicket>> GetPendingByAssetAsync(int assetId) =>
        _db.MaintenanceTickets.Where(t => t.AssetId == assetId && t.Status == TicketStatus.Pending).ToListAsync();

    public async Task<(List<MaintenanceTicket> Items, int TotalItems)> GetByReporterPagedAsync(
        int reportedByUserId, int page, int pageSize)
    {
        var query = _db.MaintenanceTickets.AsNoTracking()
            .Include(t => t.Asset)
            .Include(t => t.Technician)
            .Where(t => t.ReportedByUserId == reportedByUserId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.ReportedDate).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public Task<bool> TechnicianExistsAsync(int userId) =>
        _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role.Name == TechnicianRoleName);

    public Task<string?> GetUserFullNameAsync(int userId) =>
        _db.Users.Where(u => u.Id == userId).Select(u => (string?)u.FullName).FirstOrDefaultAsync();

    public Task<List<MaintenanceTicketHistory>> GetHistoryAsync(int ticketId) =>
        _db.MaintenanceTicketHistories.AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .ToListAsync();

    public async Task<(List<MaintenanceTicket> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, TicketStatus? status, TicketPriority? priority, bool? closed,
        DateTime? fromDate, DateTime? toDate, int? technicianScopeUserId, int page, int pageSize, string? keyword,
        TicketListExtras extras)
    {
        var query = _db.MaintenanceTickets.AsNoTracking()
            .Include(t => t.Asset)
            .Include(t => t.Technician)
            .Include(t => t.ReportedBy)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        }

        // Technician chỉ thấy phiếu Admin đã phân công cho mình — không thấy phiếu chưa phân công hay của người khác.
        if (technicianScopeUserId.HasValue)
        {
            var me = technicianScopeUserId.Value;
            query = query.Where(t => t.TechnicianId == me);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(t =>
                t.Asset.AssetCode.Contains(kw) || t.Asset.Name.Contains(kw) || t.IssueDescription.Contains(kw) ||
                (t.ReportedBy != null && t.ReportedBy.FullName.Contains(kw)) ||
                (t.Technician != null && t.Technician.FullName.Contains(kw)));
        }

        if (assetId.HasValue)
        {
            query = query.Where(t => t.AssetId == assetId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        // closed = true: phiếu đã xử lý (Resolved/Failed); false: phiếu chưa xử lý (Pending).
        if (closed.HasValue)
        {
            query = closed.Value
                ? query.Where(t => t.Status != TicketStatus.Pending)
                : query.Where(t => t.Status == TicketStatus.Pending);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.ReportedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.ReportedDate < toDate.Value);
        }

        if (extras.TechnicianId.HasValue)
        {
            query = query.Where(t => t.TechnicianId == extras.TechnicianId.Value);
        }

        if (extras.Unassigned == true)
        {
            query = query.Where(t => t.TechnicianId == null);
        }

        if (extras.CategoryId.HasValue)
        {
            query = query.Where(t => t.Asset.CategoryId == extras.CategoryId.Value);
        }

        if (extras.PendingBefore.HasValue)
        {
            query = query.Where(t => t.Status == TicketStatus.Pending && t.ReportedDate <= extras.PendingBefore.Value);
        }

        var total = await query.CountAsync();
        var ordered = extras.Sort switch
        {
            "newest" => query.OrderByDescending(t => t.ReportedDate).ThenByDescending(t => t.Id),
            "oldest" => query.OrderBy(t => t.ReportedDate).ThenBy(t => t.Id),
            // Mặc định: việc đang chờ xử lý lên trước; trong đó gấp nhất trước (Khẩn cấp -> Cao -> Bình thường -> Thấp),
            // cùng mức thì phiếu báo sớm nhất (chờ lâu nhất) trước. Phiếu đã đóng nằm sau, mới nhất trước.
            _ => query
                .OrderBy(t => t.Status != TicketStatus.Pending)
                .ThenByDescending(t => t.Status == TicketStatus.Pending ? (int)t.Priority : 0)
                .ThenBy(t => t.Status == TicketStatus.Pending ? t.ReportedDate : (DateTime?)null)
                .ThenByDescending(t => t.ReportedDate)
                .ThenByDescending(t => t.Id),
        };
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<List<MaintenanceTicketStatRow>> GetStatRowsAsync(
        int? departmentId, int? assetId, DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.MaintenanceTickets.AsNoTracking().AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        }

        if (assetId.HasValue)
        {
            query = query.Where(t => t.AssetId == assetId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.ReportedDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.ReportedDate < toDate.Value);
        }

        return query
            .Select(t => new MaintenanceTicketStatRow
            {
                AssetId = t.AssetId,
                AssetCode = t.Asset.AssetCode,
                Status = t.Status,
                ReportedDate = t.ReportedDate,
                ResolvedDate = t.ResolvedDate,
            })
            .ToListAsync();
    }
}
