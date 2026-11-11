using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IEmployeeRepository
{
    // Tracked, kèm Department — Service sửa trực tiếp rồi SaveChangesAsync.
    Task<Employee?> GetByIdAsync(int id);

    // filter.ExcludeStaffAccounts: ẩn hồ sơ gắn tài khoản Admin IT / Manager / Technician (Manager chỉ thấy nhân viên thường của phòng mình).
    Task<(List<Employee> Items, int TotalItems)> GetPagedAsync(EmployeeListFilter filter, int page, int pageSize);

    // Các chức danh đang dùng (thường gặp nhất trước) — gợi ý khi nhập chức danh để mọi người dùng cùng một cách viết.
    Task<List<string>> GetPositionSuggestionsAsync(int? departmentId, bool excludeStaffAccounts, int take);

    // Nhân viên đang hoạt động CHƯA có tài khoản (không tracking, kèm Department) — nguồn chọn khi tạo tài khoản vai trò Nhân viên.
    Task<List<Employee>> GetAccountCandidatesAsync(string? keyword, int take);

    Task<bool> NameExistsInDepartmentAsync(string fullName, int departmentId, int? excludeId = null);
    Task<bool> DepartmentExistsAsync(int departmentId);
    // Email đã có ở hồ sơ nhân viên khác (không phân biệt hoa/thường theo collation của MySQL).
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);
    Task<bool> HasOpenAllocationAsync(int employeeId);
    Task<bool> HasAnyAllocationAsync(int employeeId);

    // ----- Cổng nhân viên (tài khoản vai trò Employee) -----
    // Nhân viên gắn với tài khoản đăng nhập (không tracking, kèm Department).
    Task<Employee?> GetByUserIdAsync(int userId);
    // Như trên nhưng CÓ tracking — Service tách liên kết (UserId = null) khi xoá tài khoản.
    Task<Employee?> GetByUserIdTrackedAsync(int userId);
    // Nhân viên có đang giữ (phân bổ đang mở) đúng tài sản này không.
    Task<bool> HasOpenAllocationForAssetAsync(int employeeId, int assetId);
    // Các phân bổ đang mở của nhân viên, kèm Asset + Category (không tracking).
    Task<List<AssetAllocation>> GetOpenAllocationsWithAssetAsync(int employeeId);
    // Trong số các tài sản truyền vào, tài sản nào đang có phiếu bảo trì chờ xử lý.
    Task<HashSet<int>> GetAssetIdsWithPendingTicketsAsync(IEnumerable<int> assetIds);
    Task AddAsync(Employee employee);
    void Remove(Employee employee);
    Task<int> SaveChangesAsync();
}
