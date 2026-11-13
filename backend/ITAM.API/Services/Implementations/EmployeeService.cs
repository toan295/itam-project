using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using Microsoft.EntityFrameworkCore;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class EmployeeService : IEmployeeService
{
    private const string ManagerRoleName = "Manager";
    private const string AdminRoleName = "Admin IT";
    private const string EmployeeRoleName = "Employee";
    private const int NoAccessSentinelDepartmentId = -1;

    private readonly IEmployeeRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IAccountActivationService _activation;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IRoleRepository _roleRepo;
    private readonly IExclusiveSection _exclusive;

    public EmployeeService(
        IEmployeeRepository repo,
        IUserRepository userRepo,
        IAccountActivationService activation,
        ILogger<EmployeeService> logger,
        IRoleRepository roleRepo,
        IExclusiveSection exclusive)
    {
        _roleRepo = roleRepo;
        _exclusive = exclusive;
        _repo = repo;
        _userRepo = userRepo;
        _activation = activation;
        _logger = logger;
    }

    public async Task<PagedResultDto<EmployeeResponseDto>> GetPagedAsync(
        EmployeeListFilter filter, int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        ValidateFilter(filter);

        // Manager chỉ thấy nhân viên thường của phòng mình: không thấy phòng ban khác, cũng không thấy hồ sơ của Admin IT/Manager/Technician.
        var manager = IsManager(currentUserRole);
        filter.DepartmentId = manager ? currentUserDepartmentId ?? NoAccessSentinelDepartmentId : filter.DepartmentId;
        filter.ExcludeStaffAccounts = manager || filter.ExcludeStaffAccounts;   // receiversOnly (màn Phân bổ): chỉ nhân viên thường

        var (items, total) = await _repo.GetPagedAsync(filter, page, pageSize);
        return new PagedResultDto<EmployeeResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public Task<List<string>> GetPositionSuggestionsAsync(string? currentUserRole, int? currentUserDepartmentId)
    {
        var manager = IsManager(currentUserRole);
        return _repo.GetPositionSuggestionsAsync(
            manager ? currentUserDepartmentId ?? NoAccessSentinelDepartmentId : null, manager, take: 50);
    }

    private static void ValidateFilter(EmployeeListFilter f)
    {
        if (f.AccountState is not null && !EmployeeListFilter.AccountStates.Contains(f.AccountState))
        {
            throw new ArgumentException($"Tình trạng tài khoản không hợp lệ (chấp nhận: {string.Join(", ", EmployeeListFilter.AccountStates)}).");
        }

        if (f.RoleName is not null && !EmployeeListFilter.RoleNames.Contains(f.RoleName))
        {
            throw new ArgumentException($"Vai trò không hợp lệ (chấp nhận: {string.Join(", ", EmployeeListFilter.RoleNames)}).");
        }

        if (f.Sort is not null && !EmployeeListFilter.Sorts.Contains(f.Sort))
        {
            throw new ArgumentException($"Cách sắp xếp không hợp lệ (chấp nhận: {string.Join(", ", EmployeeListFilter.Sorts)}).");
        }
    }

    public async Task<EmployeeResponseDto> GetByIdAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);
        return MapToDto(employee);
    }

    public async Task<EmployeeResponseDto> CreateAsync(
        UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId)
    {
        EnsureDepartmentAllowed(dto.DepartmentId, currentUserRole, currentUserDepartmentId);
        await EnsureDepartmentExistsAsync(dto.DepartmentId);

        var name = PersonNameNormalizer.Normalize(dto.FullName);
        await EnsureNameAvailableAsync(name, dto.DepartmentId, excludeId: null);
        await EnsureEmailAvailableAsync(dto.Email.Trim(), excludeEmployeeId: null, linkedUserId: null);

        var employee = new Employee
        {
            // Mã tạm duy nhất cho tới khi có Id; ngay sau đó đặt lại thành NV{Id:D4}.
            EmployeeCode = "TMP-" + Guid.NewGuid().ToString("N")[..12],
            FullName = name,
            DepartmentId = dto.DepartmentId,
            Position = NormalizeOptional(dto.Position),
            Email = dto.Email.Trim(),
            Phone = PhoneNumber.Normalize(dto.Phone),
            IsActive = true,
        };

        await _repo.AddAsync(employee);
        await _repo.SaveChangesAsync();

        employee.EmployeeCode = $"NV{employee.Id:D4}";
        await _repo.SaveChangesAsync();

        var created = await _repo.GetByIdAsync(employee.Id) ?? throw new EmployeeNotFoundException(employee.Id);
        return MapToDto(created);
    }

    // Ngừng hoạt động hồ sơ của Admin IT cũng khoá tài khoản đó -> dùng chung khoá vai trò Admin IT với UserService
    // để hai thao tác đồng thời không cùng vượt qua kiểm tra "còn Admin IT khác".
    public async Task<EmployeeResponseDto> UpdateAsync(
        int id, UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId = null)
    {
        var adminRoleId = await _roleRepo.GetIdByNameAsync(AdminRoleName);
        return adminRoleId is null
            ? await UpdateCoreAsync(id, dto, currentUserRole, currentUserDepartmentId, currentUserId)
            : await _exclusive.RunAsync(LockTarget.Role, adminRoleId.Value,
                () => UpdateCoreAsync(id, dto, currentUserRole, currentUserDepartmentId, currentUserId));
    }

    private async Task<EmployeeResponseDto> UpdateCoreAsync(
        int id, UpsertEmployeeRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var employee = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);
        if (!dto.IsActive && currentUserId.HasValue && employee.UserId == currentUserId)
        {
            throw new EmployeeSelfDeactivationException();
        }

        EnsureDepartmentAllowed(dto.DepartmentId, currentUserRole, currentUserDepartmentId);
        await EnsureDepartmentExistsAsync(dto.DepartmentId);

        var name = PersonNameNormalizer.Normalize(dto.FullName);
        await EnsureNameAvailableAsync(name, dto.DepartmentId, excludeId: id);
        await EnsureEmailAvailableAsync(dto.Email.Trim(), excludeEmployeeId: id, linkedUserId: employee.UserId);

        // Đang giữ tài sản thì không được ngừng hoạt động hay chuyển phòng ban: biên bản đã in và phân bổ đang mở
        // gắn với người này ở phòng ban hiện tại; phải thu hồi tài sản trước.
        if ((employee.IsActive && !dto.IsActive || employee.DepartmentId != dto.DepartmentId)
            && await _repo.HasOpenAllocationAsync(id))
        {
            throw new EmployeeConflictException(
                "Nhân viên này đang giữ tài sản. Hãy thu hồi tài sản trước khi ngừng hoạt động hoặc chuyển phòng ban.");
        }

        var email = dto.Email.Trim();
        var position = NormalizeOptional(dto.Position);
        var phone = PhoneNumber.Normalize(dto.Phone);

        employee.FullName = name;
        employee.DepartmentId = dto.DepartmentId;
        employee.Position = position;
        employee.Email = email;
        employee.Phone = phone;
        employee.IsActive = dto.IsActive;

        // Nhân viên đã có tài khoản đăng nhập: giữ thông tin (và trạng thái hoạt động) của tài khoản khớp với nhân viên.
        // Cùng một lần lưu với bản ghi nhân viên nên không bao giờ lệch nhau.
        await SyncLinkedAccountAsync(employee, name, email, phone);

        // Entity này đang được track (đọc kèm Include): chỉ cần đổi khoá ngoại, EF tự cập nhật navigation. KHÔNG đặt
        // employee.Department = null — navigation null + khoá ngoại bắt buộc làm EF báo "quan hệ bị cắt" (lỗi 500).
        await _repo.SaveChangesAsync();

        var updated = await _repo.GetByIdAsync(id) ?? throw new EmployeeNotFoundException(id);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);

        if (employee.UserId.HasValue)
        {
            throw new EmployeeConflictException(
                "Nhân viên đã có tài khoản đăng nhập nên không thể xoá. Hãy chuyển sang trạng thái ngừng hoạt động (tài khoản sẽ bị khoá).");
        }

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

    private async Task SyncLinkedAccountAsync(Employee employee, string name, string email, string? phone)
    {
        if (!employee.UserId.HasValue)
        {
            return;
        }

        var user = await _userRepo.GetByIdTrackedAsync(employee.UserId.Value);
        if (user is null)
        {
            return;
        }

        // Email đổi khi tài khoản còn chờ kích hoạt: liên kết đã gửi tới địa chỉ cũ phải hết hiệu lực (kẻo người nhận nhầm kích hoạt
        // được tài khoản). Admin IT bấm "Gửi lại email kích hoạt" để gửi tới địa chỉ mới.
        if (user.PendingActivation && !string.Equals(user.Email, email, StringComparison.Ordinal))
        {
            await _activation.InvalidatePendingAsync(user.Id);
        }

        // Ngừng hoạt động hồ sơ gắn với Admin IT cuối cùng sẽ khoá luôn tài khoản đó -> không còn ai quản trị được hệ thống.
        if (!employee.IsActive && user.IsActive
            && string.Equals(user.Role?.Name, AdminRoleName, StringComparison.Ordinal)
            && await _userRepo.CountActiveUsersInRoleAsync(user.RoleId, excludeUserId: user.Id) == 0)
        {
            throw new EmployeeConflictException("Đây là hồ sơ của Admin IT đang hoạt động cuối cùng — không thể ngừng hoạt động.");
        }

        user.FullName = name;
        user.Email = email;
        user.Phone = phone;
        user.DepartmentId = employee.DepartmentId;
        user.IsActive = employee.IsActive;   // ngừng hoạt động => khoá tài khoản; kích hoạt lại => mở khoá.
    }

    private async Task<Employee> GetScopedAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var employee = await _repo.GetByIdAsync(id) ?? throw new EmployeeNotFoundException(id);
        // Ngoài phòng ban của Manager, hoặc hồ sơ gắn tài khoản Admin IT/Manager/Technician -> 404 như thể không tồn tại.
        if (IsManager(currentUserRole)
            && (employee.DepartmentId != currentUserDepartmentId || HasStaffAccount(employee)))
        {
            throw new EmployeeNotFoundException(id);
        }

        return employee;
    }

    // Hồ sơ gắn tài khoản quản trị/vận hành (Admin IT, Manager, Technician).
    private static bool HasStaffAccount(Employee employee)
    {
        var accountRole = employee.User?.Role?.Name;
        return accountRole is not null && !string.Equals(accountRole, EmployeeRoleName, StringComparison.Ordinal);
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

    // Email là định danh đăng nhập của tài khoản sẽ tạo cho nhân viên nên phải duy nhất: không trùng hồ sơ nhân viên khác,
    // cũng không trùng tài khoản đang tồn tại (trừ chính tài khoản đang gắn với hồ sơ này).
    private async Task EnsureEmailAvailableAsync(string email, int? excludeEmployeeId, int? linkedUserId)
    {
        if (await _repo.EmailExistsAsync(email, excludeEmployeeId))
        {
            throw new EmployeeConflictException($"Email {email} đã được dùng cho một nhân viên khác.");
        }

        if (await _userRepo.GetByEmailAsync(email) is { } other && other.Id != linkedUserId)
        {
            throw new EmployeeConflictException($"Email {email} đã được dùng cho một tài khoản khác.");
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

    // Vai trò bị giới hạn theo phòng ban: Manager, và Employee (khi Admin IT cấp thêm quyền cho vai trò Nhân viên).
    private static bool IsManager(string? role) => string.Equals(role, ManagerRoleName, StringComparison.Ordinal) || string.Equals(role, "Employee", StringComparison.Ordinal);

    private static EmployeeResponseDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullName = e.FullName,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name ?? "",
        Position = e.Position,
        Email = e.Email,
        Phone = e.Phone,
        HasAccount = e.UserId.HasValue,
        AccountUserId = e.UserId,
        RoleName = e.User?.Role?.Name,
        PendingActivation = e.User?.PendingActivation ?? false,
        AccountIsActive = e.User?.IsActive,
        IsActive = e.IsActive,
    };
}
