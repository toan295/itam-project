using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Employees;

namespace ITAM.API.Services.Interfaces;

// Cổng dành cho tài khoản nhân viên (vai trò Employee): xem tài sản được cấp và các yêu cầu bảo trì của chính mình.
// Việc TẠO yêu cầu dùng POST /maintenance-tickets (MaintenanceTicketService kiểm tra tài sản có thuộc nhân viên không).
public interface IEmployeePortalService
{
    Task<List<MyAssetDto>> GetMyAssetsAsync(int userId);
    Task<PagedResultDto<MyTicketDto>> GetMyTicketsAsync(int userId, int page, int pageSize);
}
