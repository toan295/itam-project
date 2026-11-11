using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Employees;

namespace ITAM.API.Services.Interfaces;

public interface IEmployeeService
{
    // Manager chỉ làm việc trong phòng ban của mình; Admin IT toàn quyền.
    Task<PagedResultDto<EmployeeResponseDto>> GetPagedAsync(
        EmployeeListFilter filter, int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId);

    // Gợi ý chức danh (đã dùng trong phạm vi người gọi được xem).
    Task<List<string>> GetPositionSuggestionsAsync(string? currentUserRole, int? currentUserDepartmentId);

    Task<EmployeeResponseDto> GetByIdAsync(int id, string? currentUserRole, int? currentUserDepartmentId);
    Task<EmployeeResponseDto> CreateAsync(UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId);
    Task<EmployeeResponseDto> UpdateAsync(
        int id, UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId = null);
    Task DeleteAsync(int id, string? currentUserRole, int? currentUserDepartmentId);
}
