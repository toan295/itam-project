using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public interface IMaintenanceTicketRepository
{
    Task<MaintenanceTicket?> GetByIdWithDetailsAsync(int id); // Include(Asset), Include(Technician)
    Task AddAsync(MaintenanceTicket ticket);
    void Update(MaintenanceTicket ticket);
    Task<int> SaveChangesAsync();
    Task<bool> TechnicianExistsAsync(int userId); // User tồn tại, đang hoạt động và Role.Name == "Technician"

    // status đã được Service parse & validate trước — Repository chỉ lọc dữ liệu, không chứa nghiệp vụ.
    // departmentId lọc theo phòng ban của TÀI SẢN (MaintenanceTicket không có DepartmentId riêng).
    // toDate là cận trên loại trừ (Service đã cộng 1 ngày).
    // orAssignedTechnicianId: khi có giá trị, bộ lọc phòng ban trở thành "phòng ban HOẶC phiếu được gán cho
    // kỹ thuật viên này" — kỹ thuật viên được gán phiếu của phòng ban khác vẫn phải thấy phiếu của mình.
    Task<(List<MaintenanceTicket> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, TicketStatus? status, DateTime? fromDate, DateTime? toDate,
        int? orAssignedTechnicianId, int page, int pageSize);
}
