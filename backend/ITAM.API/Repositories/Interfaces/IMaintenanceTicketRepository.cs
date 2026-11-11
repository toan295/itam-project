using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Models;

namespace ITAM.API.Repositories.Interfaces;

public interface IMaintenanceTicketRepository
{
    Task<MaintenanceTicket?> GetByIdWithDetailsAsync(int id); // Include(Asset), Include(Technician)
    Task AddAsync(MaintenanceTicket ticket);
    void Update(MaintenanceTicket ticket);
    Task<int> SaveChangesAsync();
    Task<bool> TechnicianExistsAsync(int userId);
    Task<string?> GetUserFullNameAsync(int userId);

    // Lịch sử xử lý của phiếu, cũ nhất trước.
    Task<List<MaintenanceTicketHistory>> GetHistoryAsync(int ticketId); // User tồn tại, đang hoạt động và Role.Name == "Technician"

    // status đã được Service parse & validate trước — Repository chỉ lọc dữ liệu, không chứa nghiệp vụ.
    // departmentId lọc theo phòng ban của TÀI SẢN (MaintenanceTicket không có DepartmentId riêng).
    // toDate là cận trên loại trừ (Service đã cộng 1 ngày).
    // technicianScopeUserId: khi có giá trị (người xem là Technician), chỉ trả phiếu được gán cho người đó.
    // keyword: tìm theo mã/tên tài sản, nội dung báo lỗi, tên người báo, tên kỹ thuật viên.
    Task<(List<MaintenanceTicket> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, TicketStatus? status, TicketPriority? priority, bool? closed,
        DateTime? fromDate, DateTime? toDate, int? technicianScopeUserId, int page, int pageSize, string? keyword,
        TicketListExtras extras);

    // Phiếu do một người dùng tạo (nhân viên xem yêu cầu của chính mình), mới nhất trước.
    Task<(List<MaintenanceTicket> Items, int TotalItems)> GetByReporterPagedAsync(int reportedByUserId, int page, int pageSize);

    // Tài sản đang có phiếu chờ xử lý (Pending) hay không / có phiếu Pending nào KHÁC phiếu đang đóng hay không.
    Task<bool> HasPendingTicketAsync(int assetId);
    Task<bool> HasOtherPendingTicketsAsync(int assetId, int excludeTicketId);

    // TRACKED — Service sửa trực tiếp (đóng phiếu tự động khi tài sản đổi trạng thái/bị thanh lý) rồi lưu cùng lúc.
    Task<List<MaintenanceTicket>> GetPendingByAssetAsync(int assetId);

    // UC-13: các dòng rút gọn đã lọc; Service tự tổng hợp (đếm theo Status, trung bình, theo tài sản-năm).
    // departmentId lọc theo phòng ban của tài sản; toDate là cận trên loại trừ.
    Task<List<MaintenanceTicketStatRow>> GetStatRowsAsync(
        int? departmentId, int? assetId, DateTime? fromDate, DateTime? toDate);
}
