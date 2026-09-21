using ITAM.API.Data;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
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

    public Task<bool> TechnicianExistsAsync(int userId) =>
        _db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Role.Name == TechnicianRoleName);

    public async Task<(List<MaintenanceTicket> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, TicketStatus? status, DateTime? fromDate, DateTime? toDate,
        int? orAssignedTechnicianId, int page, int pageSize)
    {
        var query = _db.MaintenanceTickets.AsNoTracking()
            .Include(t => t.Asset)
            .Include(t => t.Technician)
            .AsQueryable();

        if (departmentId.HasValue && orAssignedTechnicianId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value
                || t.TechnicianId == orAssignedTechnicianId.Value);
        }
        else if (departmentId.HasValue)
        {
            query = query.Where(t => t.Asset.DepartmentId == departmentId.Value);
        }

        if (assetId.HasValue)
        {
            query = query.Where(t => t.AssetId == assetId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
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
            .OrderByDescending(t => t.ReportedDate)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
