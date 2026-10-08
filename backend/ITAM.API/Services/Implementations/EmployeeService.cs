using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class EmployeeService : IEmployeeService
{
    private const string ManagerRoleName = "Manager";
    private const int NoAccessSentinelDepartmentId = -1;

    private readonly IEmployeeRepository _repo;

    public EmployeeService(IEmployeeRepository repo)
    {
        _repo = repo;
    }

    public async Task<PagedResultDto<EmployeeResponseDto>> GetPagedAsync(
        int? departmentId, string? keyword, bool? isActive, int page, int pageSize,
        string? currentUserRole, int? currentUserDepartmentId)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var scoped = IsManager(currentUserRole) ? currentUserDepartmentId ?? NoAccessSentinelDepartmentId : departmentId;

        var (items, total) = await _repo.GetPagedAsync(scoped, keyword, isActive, page, pageSize);
        return new PagedResultDto<EmployeeResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<EmployeeResponseDto> GetByIdAsync(int id, string? currentUserRole, int? currentUserDepartmentId) =>
        MapToDto(await GetScopedAsync(id, currentUserRole, currentUserDepartmentId));

    public async Task<EmployeeResponseDto> CreateAsync(
        UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId)
    {
        EnsureDepartmentAllowed(dto.DepartmentId, currentUserRole, currentUserDepartmentId);
        await EnsureDepartmentExistsAsync(dto.DepartmentId);

        var name = PersonNameNormalizer.Normalize(dto.FullName);
        await EnsureNameAvailableAsync(name, dto.DepartmentId, excludeId: null);

        var employee = new Employee
        {
            // Mã tạm duy nhất cho tới khi có Id; ngay sau đó đặt lại thành NV{Id:D4}.
            EmployeeCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            FullName = name,
            DepartmentId = dto.DepartmentId,
            Position = NormalizeOptional(dto.Position),
            IsActive = true,
        };

        await _repo.AddAsync(employee);
        await _repo.SaveChangesAsync();

        employee.EmployeeCode = $"NV{employee.Id:D4}";
        await _repo.SaveChangesAsync();

        return MapToDto(await _repo.GetByIdAsync(employee.Id) ?? throw new EmployeeNotFoundException(employee.Id));
    }

    public async Task<EmployeeResponseDto> UpdateAsync(
        int id, UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);
        EnsureDepartmentAllowed(dto.DepartmentId, currentUserRole, currentUserDepartmentId);
        await EnsureDepartmentExistsAsync(dto.DepartmentId);

        var name = PersonNameNormalizer.Normalize(dto.FullName);
        await EnsureNameAvailableAsync(name, dto.DepartmentId, excludeId: id);

        // Đang giữ tài sản thì không được ngừng hoạt động hay chuyển phòng ban: biên bản đã in và phân bổ đang mở
        // gắn với người này ở phòng ban hiện tại; phải thu hồi tài sản trước.
        if ((employee.IsActive && !dto.IsActive || employee.DepartmentId != dto.DepartmentId)
            && await _repo.HasOpenAllocationAsync(id))
        {
            throw new EmployeeConflictException(
                "Nhân viên này đang giữ tài sản. Hãy thu hồi tài sản trước khi ngừng hoạt động hoặc chuyển phòng ban.");
        }

        employee.FullName = name;
        employee.DepartmentId = dto.DepartmentId;
        employee.Position = NormalizeOptional(dto.Position);
        employee.IsActive = dto.IsActive;
        employee.Department = null!; // khoá ngoại là nguồn sự thật; nạp lại navigation bên dưới.
        await _repo.SaveChangesAsync();

        return MapToDto(await _repo.GetByIdAsync(id) ?? throw new EmployeeNotFoundException(id));
    }

    public async Task DeleteAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);

        // Có lịch sử phân bổ thì không xoá (mất dấu vết biên bản) — chỉ ngừng hoạt động.
        if (await _repo.HasAnyAllocationAsync(id))
        {
            throw new EmployeeConflictException(
                "Nhân viên đã có lịch sử phân bổ tài sản nên không thể xoá. Hãy chuyển sang trạng thái ngừng hoạt động.");
        }

        _repo.Remove(employee);
        await _repo.SaveChangesAsync();
    }

    // ----- helpers -----

    private async Task<Employee> GetScopedAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await _repo.GetByIdAsync(id) ?? throw new EmployeeNotFoundException(id);
        // Ngoài phòng ban của Manager -> 404 như thể không tồn tại.
        if (IsManager(currentUserRole) && employee.DepartmentId != currentUserDepartmentId)
        {
            throw new EmployeeNotFoundException(id);
        }

        return employee;
    }

    private static void EnsureDepartmentAllowed(int departmentId, string? currentUserRole, int? currentUserDepartmentId)
    {
        if (IsManager(currentUserRole) && departmentId != currentUserDepartmentId)
        {
            throw new DepartmentForbiddenException();
        }
    }

    private async Task EnsureDepartmentExistsAsync(int departmentId)
    {
        if (!await _repo.DepartmentExistsAsync(departmentId))
        {
            throw new ArgumentException($"Phòng ban (DepartmentId={departmentId}) không tồn tại.");
        }
    }

    private async Task EnsureNameAvailableAsync(string name, int departmentId, int? excludeId)
    {
        if (await _repo.NameExistsInDepartmentAsync(name, departmentId, excludeId))
        {
            throw new EmployeeConflictException(
                $"Đã có nhân viên \"{name}\" trong phòng ban này. Nếu là người khác trùng tên, hãy thêm chức danh/ký hiệu phân biệt vào họ tên.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");

    private static bool IsManager(string? role) => string.Equals(role, ManagerRoleName, StringComparison.Ordinal);

    private static EmployeeResponseDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullName = e.FullName,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name ?? "",
        Position = e.Position,
        IsActive = e.IsActive,
    };
}
