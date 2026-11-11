using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class EmployeePortalService : IEmployeePortalService
{
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IMaintenanceTicketRepository _ticketRepo;

    public EmployeePortalService(IEmployeeRepository employeeRepo, IMaintenanceTicketRepository ticketRepo)
    {
        _employeeRepo = employeeRepo;
        _ticketRepo = ticketRepo;
    }

    public async Task<List<MyAssetDto>> GetMyAssetsAsync(int userId)
    {
        // Tài khoản chưa gắn nhân viên (hoặc nhân viên đã ngừng hoạt động) -> không có tài sản nào.
        var employee = await _employeeRepo.GetByUserIdAsync(userId);
        if (employee is null || !employee.IsActive)
        {
            return new List<MyAssetDto>();
        }

        var allocations = await _employeeRepo.GetOpenAllocationsWithAssetAsync(employee.Id);
        var pending = await _employeeRepo.GetAssetIdsWithPendingTicketsAsync(allocations.Select(a => a.AssetId));

        return allocations.Select(a => new MyAssetDto
        {
            AllocationId = a.Id,
            AssetId = a.AssetId,
            AssetCode = a.Asset.AssetCode,
            AssetName = a.Asset.Name,
            CategoryName = a.Asset.Category?.Name ?? "",
            SerialNumber = a.Asset.SerialNumber,
            AllocatedDate = a.AllocatedDate,
            AssetStatus = a.Asset.Status.ToString(),
            HasPendingTicket = pending.Contains(a.AssetId),
        }).ToList();
    }

    public async Task<PagedResultDto<MyTicketDto>> GetMyTicketsAsync(int userId, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var (items, total) = await _ticketRepo.GetByReporterPagedAsync(userId, page, pageSize);

        return new PagedResultDto<MyTicketDto>
        {
            Items = items.Select(t => new MyTicketDto
            {
                Id = t.Id,
                AssetCode = t.Asset?.AssetCode ?? "",
                AssetName = t.Asset?.Name ?? "",
                IssueDescription = t.IssueDescription,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                TechnicianName = t.Technician?.FullName,
                ReportedDate = t.ReportedDate,
                ResolvedDate = t.ResolvedDate,
                Notes = t.Notes,
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }
}
