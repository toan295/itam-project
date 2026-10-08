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

    public Task<bool> TechnicianExistsAsync(int userId) =>
        _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role.Name == TechnicianRoleName);

    public async Task<(List<MaintenanceTicket> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, TicketStatus? status, TicketPriority? priority, bool? closed,
        DateTime? fromDate, DateTime? toDate, int? technicianScopeUserId, int page, int pageSize)
    {
        var query = _db.MaintenanceTickets.AsNoTracking()
            .Include(t => t.Asset)
            .Include(t => t.Technician)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        }

        // Technician chỉ thấy phiếu được giao cho mình và phiếu chưa ai nhận (để tự nhận) — không thấy
        // phiếu/tên của kỹ thuật viên khác.
        if (technicianScopeUserId.HasValue)
        {
            var me = technicianScopeUserId.Value;
            query = query.Where(t => t.TechnicianId == me || t.TechnicianId == null);
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

        var total = await query.CountAsync();
        var items = await query
            // Việc đang chờ xử lý lên trước; trong đó gấp nhất trước (Khẩn cấp -> Cao -> Bình thường -> Thấp),
            // cùng mức thì phiếu báo sớm nhất (chờ lâu nhất) trước. Phiếu đã đóng nằm sau, mới nhất trước.
            .OrderBy(t => t.Status != TicketStatus.Pending)
            .ThenByDescending(t => t.Status == TicketStatus.Pending ? (int)t.Priority : 0)
            .ThenBy(t => t.Status == TicketStatus.Pending ? t.ReportedDate : (DateTime?)null)
            .ThenByDescending(t => t.ReportedDate)
            .ThenByDescending(t => t.Id)
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
