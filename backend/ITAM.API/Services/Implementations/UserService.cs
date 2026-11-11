using ITAM.API.Models.DTOs.Common;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class UserService : IUserService
{
    private const string AdminRoleName = "Admin IT";
    private const string TechnicianRoleName = "Technician";
    private const string EmployeeRoleName = "Employee";
    // Số kỹ thuật viên đang hoạt động khuyến nghị tối thiểu — họ phục vụ tất cả phòng ban. Chỉ cảnh báo, không chặn.
    private const int MinActiveTechnicians = 3;

    private const int BCryptWorkFactor = 11;

    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IAuditLogService _auditLogService;
    private readonly IAccountActivationService _activation;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILogger<UserService> _logger;
    private readonly IExclusiveSection _exclusive;

    public UserService(
        IUserRepository userRepo,
        IRoleRepository roleRepo,
        IDepartmentRepository departmentRepo,
        IAuditLogService auditLogService,
        IAccountActivationService activation,
        IEmployeeRepository employeeRepo,
        ILogger<UserService> logger,
        IExclusiveSection exclusive)
    {
        _exclusive = exclusive;
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _departmentRepo = departmentRepo;
        _auditLogService = auditLogService;
        _activation = activation;
        _employeeRepo = employeeRepo;
        _logger = logger;
    }

    public async Task<PagedResultDto<UserResponseDto>> GetPagedAsync(int page, int pageSize, UserListFilter filter)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        if (filter.Activation is not null && !UserListFilter.Activations.Contains(filter.Activation))
        {
            throw new ArgumentException($"Tình trạng kích hoạt không hợp lệ (chấp nhận: {string.Join(", ", UserListFilter.Activations)}).");
        }

        if (filter.Sort is not null && !UserListFilter.Sorts.Contains(filter.Sort))
        {
            throw new ArgumentException($"Cách sắp xếp không hợp lệ (chấp nhận: {string.Join(", ", UserListFilter.Sorts)}).");
        }

        var (items, total) = await _userRepo.GetPagedAsync(page, pageSize, filter);
        var activations = await _activation.GetStatusesAsync(items.Select(u => u.Id));
        return new PagedResultDto<UserResponseDto>
        {
            Items = items.Select(u => MapToDto(u, activations.GetValueOrDefault(u.Id))).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<UserResponseDto> GetByIdAsync(int id)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        return MapToDto(user, await GetActivationStatusAsync(user));
    }

    // Mọi tài khoản đều tạo từ một nhân viên có sẵn (1-1). Tài khoản chờ kích hoạt: hệ thống gửi email chứa liên kết, người dùng
    // bấm "Kích hoạt tài khoản" và tự đặt mật khẩu — Admin IT không biết và không đặt mật khẩu của ai.
    public async Task<UserResponseDto> CreateAsync(CreateUserRequestDto dto, int currentUserId)
    {
        if (dto.EmployeeId <= 0)
        {
            throw new ArgumentException(
                "Phải chọn nhân viên: tài khoản chỉ tạo cho nhân viên đã có trong Danh mục nhân viên. Hãy tạo nhân viên trước.");
        }

        if (!await _roleRepo.ExistsAsync(dto.RoleId))
        {
            throw new ArgumentException($"Vai trò (RoleId={dto.RoleId}) không tồn tại.");
        }

        var employee = await _employeeRepo.GetByIdAsync(dto.EmployeeId)
            ?? throw new ArgumentException("Nhân viên không tồn tại. Hãy tạo nhân viên ở Danh mục nhân viên trước.");

        if (employee.UserId.HasValue)
        {
            throw new InvalidOperationException("Nhân viên này đã có tài khoản đăng nhập.");
        }

        if (!employee.IsActive)
        {
            throw new ArgumentException("Nhân viên đã ngừng hoạt động — hãy kích hoạt lại nhân viên trước khi tạo tài khoản.");
        }

        if (string.IsNullOrWhiteSpace(employee.Email))
        {
            throw new ArgumentException("Nhân viên chưa có email. Hãy bổ sung email ở Danh mục nhân viên trước khi tạo tài khoản.");
        }

        var email = employee.Email.Trim();
        if (await _userRepo.GetByEmailAsync(email) is not null)
        {
            throw new EmailAlreadyExistsException(email);
        }

        var user = new User
        {
            FullName = employee.FullName,
            Email = email,
            Phone = employee.Phone,
            // Mật khẩu ngẫu nhiên không ai biết: tài khoản chỉ dùng được sau khi người dùng tự đặt mật khẩu qua liên kết kích hoạt.
            PasswordHash = RandomPasswordHash(),
            MustChangePassword = false,
            PendingActivation = true,
            RoleId = dto.RoleId,
            DepartmentId = employee.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        // Tạo tài khoản + liên kết với nhân viên trong CÙNG một lần lưu (employee đang được track): không thể sinh tài khoản mồ côi.
        employee.User = user;
        await _userRepo.AddAsync(user);
        await SaveChangesGuardingEmailConflictAsync(email);

        var created = await _userRepo.GetByIdWithDetailsAsync(user.Id) ?? throw new UserNotFoundException(user.Id);
        _logger.LogInformation("Tạo tài khoản {Email} ({Role}, chờ kích hoạt) cho {EmployeeCode}", created.Email, created.Role?.Name, employee.EmployeeCode);

        // Lần lưu thứ 2 (bắt buộc): EntityId của log là Id tự sinh, chỉ có sau lần lưu User ở trên.
        await _auditLogService.RecordAsync(
            currentUserId, "Create", "User", created.Id, oldValue: null, newValue: ToAuditSnapshot(created));
        await _userRepo.SaveChangesAsync();

        // Gửi sau khi đã lưu: gửi mail lỗi không được làm mất tài khoản — Admin thấy cảnh báo và bấm "Gửi lại email kích hoạt".
        var dispatch = await TryDispatchActivationAsync(created.Id, IAccountActivationService.ReasonCreated);
        var result = MapToDto(created, await GetActivationStatusAsync(created));
        result.ActivationDispatch = dispatch;
        return result;
    }

    // Hạ/khoá/xoá Admin IT được tuần tự hoá bằng khoá dòng vai trò Admin IT: nếu không, hai Admin cùng hạ nhau một lúc
    // đều thấy "còn Admin khác" rồi cùng thành công và hệ thống không còn quản trị viên nào.
    private async Task<T> InAdminLockAsync<T>(Func<Task<T>> work)
    {
        var adminRoleId = await _roleRepo.GetIdByNameAsync(AdminRoleName);
        return adminRoleId is null ? await work() : await _exclusive.RunAsync(LockTarget.Role, adminRoleId.Value, work);
    }

    public Task<UserResponseDto> UpdateAsync(int id, UpdateUserRequestDto dto, int currentUserId) =>
        InAdminLockAsync(() => UpdateCoreAsync(id, dto, currentUserId));

    private async Task<UserResponseDto> UpdateCoreAsync(int id, UpdateUserRequestDto dto, int currentUserId)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        var oldValue = ToAuditSnapshot(user);

        // UC-03 quy tắc nghiệp vụ: không được tự khoá tài khoản của chính mình.
        if (id == currentUserId && !dto.IsActive)
        {
            throw new SelfLockNotAllowedException();
        }

        if (!await _roleRepo.ExistsAsync(dto.RoleId))
        {
            throw new ArgumentException($"Vai trò (RoleId={dto.RoleId}) không tồn tại.");
        }

        // Tự hạ/đổi vai trò của chính mình dễ làm mất quyền quản trị ngoài ý muốn -> để Admin khác thực hiện.
        if (id == currentUserId && dto.RoleId != user.RoleId)
        {
            throw new SelfRoleChangeNotAllowedException();
        }

        // Vai trò Nhân viên chỉ dùng cổng riêng: không chuyển vào/ra vai trò này (tạo tài khoản mới nếu cần).
        var isEmployeeAccount = IsEmployeeAccount(user);
        var targetIsEmployeeRole = await _roleRepo.HasNameAsync(dto.RoleId, EmployeeRoleName);
        if (dto.RoleId != user.RoleId && (isEmployeeAccount || targetIsEmployeeRole))
        {
            throw new ArgumentException("Không đổi tài khoản sang hoặc khỏi vai trò Nhân viên. Hãy khoá tài khoản này và tạo tài khoản mới.");
        }

        var wasAdmin = string.Equals(user.Role.Name, AdminRoleName, StringComparison.Ordinal);
        var willBeInactiveOrDemoted = wasAdmin && (!dto.IsActive || dto.RoleId != user.RoleId);
        if (willBeInactiveOrDemoted)
        {
            // Đếm các Admin IT active KHÁC user này — nếu bằng 0 thì user này là Admin IT cuối cùng.
            var otherActiveAdmins = await _userRepo.CountActiveUsersInRoleAsync(user.RoleId, excludeUserId: id);
            if (otherActiveAdmins == 0)
            {
                throw new LastAdminProtectionException();
            }
        }

        string? warning = null;
        var wasTechnician = string.Equals(user.Role.Name, TechnicianRoleName, StringComparison.Ordinal);
        if (wasTechnician && user.IsActive && (!dto.IsActive || dto.RoleId != user.RoleId))
        {
            // Không chặn — chỉ cảnh báo khi việc này kéo số kỹ thuật viên hoạt động xuống dưới mức tối thiểu.
            var otherActiveTechnicians = await _userRepo.CountActiveUsersInRoleAsync(user.RoleId, excludeUserId: id);
            if (otherActiveTechnicians < MinActiveTechnicians)
            {
                warning = $"Hiện chỉ còn {otherActiveTechnicians} kỹ thuật viên đang hoạt động " +
                          $"(khuyến nghị tối thiểu {MinActiveTechnicians}). Hãy bổ sung kỹ thuật viên.";
            }
        }

        await ApplyProfileChangesAsync(user, dto.FullName, dto.Email, dto.Phone, dto.DepartmentId);

        user.RoleId = dto.RoleId;
        user.IsActive = dto.IsActive;
        _userRepo.Update(user);

        // Id đã biết trước -> ghi log rồi lưu MỘT lần: User và AuditLog cùng commit (atomic).
        await _auditLogService.RecordAsync(currentUserId, "Update", "User", user.Id, oldValue, ToAuditSnapshot(user));
        await _userRepo.SaveChangesAsync();

        var updated = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        var result = MapToDto(updated, await GetActivationStatusAsync(updated));
        result.Warning = warning;
        return result;
    }

    // Người dùng tự sửa hồ sơ của mình (mọi vai trò): chỉ họ tên và số điện thoại. Email, phòng ban, vai trò do Admin IT quản lý.
    public Task<UserResponseDto> UpdateOwnProfileAsync(int userId, string? fullName, string? phone) =>
        InAdminLockAsync(async () =>
        {
            var user = await _userRepo.GetByIdWithDetailsAsync(userId) ?? throw new UserNotFoundException(userId);
            var oldValue = ToAuditSnapshot(user);
            await ApplyProfileChangesAsync(user, fullName, null, phone, null);
            _userRepo.Update(user);
            await _auditLogService.RecordAsync(userId, "Update", "User", user.Id, oldValue, ToAuditSnapshot(user));
            await _userRepo.SaveChangesAsync();
            var updated = await _userRepo.GetByIdWithDetailsAsync(userId) ?? throw new UserNotFoundException(userId);
            return MapToDto(updated, await GetActivationStatusAsync(updated));
        });

    // Áp thông tin cá nhân mới (null = giữ nguyên) lên tài khoản và hồ sơ nhân viên gắn với nó (nếu có) — cùng một lần lưu.
    private async Task ApplyProfileChangesAsync(User user, string? fullName, string? email, string? phone, int? departmentId)
    {
        if (fullName is null && email is null && phone is null && departmentId is null)
        {
            return;
        }

        var name = fullName is null ? user.FullName : PersonNameNormalizer.Normalize(fullName);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Họ tên không được để trống.");
        }

        var newEmail = email is null ? user.Email : email.Trim();
        var emailChanged = !string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && await _userRepo.GetByEmailAsync(newEmail) is { } other && other.Id != user.Id)
        {
            throw new EmailAlreadyExistsException(newEmail);
        }

        var newPhone = phone is null ? user.Phone : PhoneNumber.Normalize(phone);
        var newDepartmentId = departmentId ?? user.DepartmentId;
        if (newDepartmentId != user.DepartmentId && await _departmentRepo.GetByIdAsync(newDepartmentId) is null)
        {
            throw new ArgumentException($"Phòng ban (DepartmentId={newDepartmentId}) không tồn tại.");
        }

        var employee = await _employeeRepo.GetByUserIdTrackedAsync(user.Id);
        if (employee is not null)
        {
            if (emailChanged && await _employeeRepo.EmailExistsAsync(newEmail, employee.Id))
            {
                throw new EmailAlreadyExistsException(newEmail);
            }

            var nameOrDeptChanged = !string.Equals(name, employee.FullName, StringComparison.Ordinal) || newDepartmentId != employee.DepartmentId;
            if (nameOrDeptChanged && await _employeeRepo.NameExistsInDepartmentAsync(name, newDepartmentId, employee.Id))
            {
                throw new ArgumentException($"Đã có nhân viên \"{name}\" trong phòng ban này. Hãy thêm ký hiệu phân biệt vào họ tên.");
            }

            if (newDepartmentId != employee.DepartmentId && await _employeeRepo.HasOpenAllocationAsync(employee.Id))
            {
                throw new ArgumentException("Người này đang giữ tài sản. Hãy thu hồi tài sản trước khi chuyển phòng ban.");
            }

            employee.FullName = name;
            employee.Email = newEmail;
            employee.Phone = newPhone;
            employee.DepartmentId = newDepartmentId;
        }

        // Email đổi khi tài khoản còn chờ kích hoạt: liên kết đã gửi tới địa chỉ cũ phải hết hiệu lực.
        if (emailChanged && user.PendingActivation)
        {
            await _activation.InvalidatePendingAsync(user.Id);
        }

        user.FullName = name;
        user.Email = newEmail;
        user.Phone = newPhone;
        user.DepartmentId = newDepartmentId;
    }

    // Xoá tài khoản = xoá mềm (xem User.IsDeleted). Nhân viên/hồ sơ vẫn còn trong Danh mục nhân viên và có thể được tạo tài khoản mới.
    public Task DeleteAsync(int id, int currentUserId) =>
        InAdminLockAsync(async () =>
        {
            await DeleteCoreAsync(id, currentUserId);
            return true;
        });

    private async Task DeleteCoreAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new SelfDeleteNotAllowedException();
        }

        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);

        // Admin IT đang hoạt động cuối cùng không được xoá (cùng quy tắc với khoá/hạ quyền).
        if (user.IsActive && string.Equals(user.Role.Name, AdminRoleName, StringComparison.Ordinal)
            && await _userRepo.CountActiveUsersInRoleAsync(user.RoleId, excludeUserId: id) == 0)
        {
            throw new LastAdminProtectionException();
        }

        var pendingTickets = await _userRepo.CountPendingAssignedTicketsAsync(id);
        if (pendingTickets > 0)
        {
            throw new UserHasPendingWorkException(pendingTickets);
        }

        var snapshot = ToAuditSnapshot(user);
        var employee = await _employeeRepo.GetByUserIdTrackedAsync(id);
        await _activation.InvalidatePendingAsync(id);

        var now = DateTime.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.IsActive = false;
        user.PendingActivation = false;
        user.MustChangePassword = false;
        user.PasswordHash = RandomPasswordHash();
        // Email gốc đã nằm trong nhật ký; đổi sang giá trị vô hiệu để giải phóng chỉ mục duy nhất và không ai đăng nhập lại được.
        user.Email = $"deleted.{user.Id}.{now:yyyyMMddHHmmss}@deleted.invalid";
        _userRepo.Update(user);
        if (employee is not null)
        {
            employee.UserId = null;
        }

        await _auditLogService.RecordAsync(currentUserId, "Delete", "User", id, snapshot, new { Deleted = true });
        await _userRepo.SaveChangesAsync();
        _logger.LogInformation("Đã xoá (mềm) tài khoản {UserId} bởi {ActorId}", id, currentUserId);
    }

    // Admin IT gửi lại email kích hoạt (email đầu bị lỗi/quá hạn/người dùng không nhận được). Liên kết cũ bị vô hiệu.
    public async Task<UserResponseDto> ResendActivationAsync(int id, int currentUserId)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        if (!user.PendingActivation)
        {
            throw new ArgumentException("Tài khoản này đã được kích hoạt.");
        }

        if (!user.IsActive)
        {
            throw new ArgumentException("Tài khoản đang bị khoá — hãy mở khoá trước khi gửi email kích hoạt.");
        }

        var dispatch = await _activation.SendAsync(id, IAccountActivationService.ReasonResent);

        await _auditLogService.RecordAsync(
            currentUserId, "ResendActivation", "User", id, oldValue: null, newValue: new { user.Email, dispatch.Sent });
        await _userRepo.SaveChangesAsync();

        var result = MapToDto(user, await GetActivationStatusAsync(user));
        result.ActivationDispatch = dispatch;
        return result;
    }

    public async Task<List<EmployeeCandidateDto>> GetEmployeeCandidatesAsync(string? keyword)
    {
        var employees = await _employeeRepo.GetAccountCandidatesAsync(keyword, take: 50);
        return employees.Select(e => new EmployeeCandidateDto
        {
            Id = e.Id,
            EmployeeCode = e.EmployeeCode,
            FullName = e.FullName,
            Position = e.Position,
            Email = e.Email,
            Phone = e.Phone,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.Department?.Name ?? "",
        }).ToList();
    }

    // Admin IT đặt lại mật khẩu HỘ người khác: mật khẩu cũ mất hiệu lực NGAY (Admin không đặt và không biết mật khẩu mới),
    // tài khoản quay về trạng thái chờ kích hoạt và hệ thống gửi liên kết để chính người đó đặt mật khẩu mới.
    public async Task<UserResponseDto> ResetPasswordAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new SelfPasswordResetNotAllowedException();
        }

        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);

        if (user.PendingActivation)
        {
            throw new ArgumentException("Tài khoản chưa được kích hoạt nên chưa có mật khẩu. Hãy bấm “Gửi lại email kích hoạt” nếu họ chưa nhận được.");
        }

        if (!user.IsActive)
        {
            throw new ArgumentException("Tài khoản đang bị khoá — hãy mở khoá trước khi đặt lại mật khẩu.");
        }

        user.PasswordHash = RandomPasswordHash();
        user.MustChangePassword = false;
        user.PendingActivation = true;
        _userRepo.Update(user);
        _logger.LogInformation("Password reset requested by Admin IT for user {Email}", user.Email);

        await _auditLogService.RecordAsync(
            currentUserId,
            "ResetPassword",
            "User",
            user.Id,
            oldValue: null,
            newValue: new { PasswordInvalidated = true, ResetLinkSentTo = user.Email });
        await _userRepo.SaveChangesAsync();

        var dispatch = await TryDispatchActivationAsync(id, IAccountActivationService.ReasonReset);
        var updated = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        var result = MapToDto(updated, await GetActivationStatusAsync(updated));
        result.ActivationDispatch = dispatch;
        return result;
    }

    private async Task<ActivationStatusDto> GetActivationStatusAsync(User user) =>
        (await _activation.GetStatusesAsync(new[] { user.Id })).GetValueOrDefault(user.Id) ?? new ActivationStatusDto();

    private static bool IsEmployeeAccount(User u) => string.Equals(u.Role?.Name, EmployeeRoleName, StringComparison.Ordinal);

    // Mật khẩu ngẫu nhiên 256 bit không ai biết (kể cả Admin IT): chỉ là chỗ giữ cho tới khi người dùng tự đặt mật khẩu.
    private static string RandomPasswordHash() => BCrypt.Net.BCrypt.HashPassword(
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), BCryptWorkFactor);

    private async Task<EmailDispatchDto> TryDispatchActivationAsync(int userId, string reason)
    {
        try
        {
            return await _activation.SendAsync(userId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không gửi được email kích hoạt cho user {UserId}", userId);
            return new EmailDispatchDto
            {
                Sent = false, Mode = "disabled",
                Message = "Không gửi được email kích hoạt do lỗi hệ thống. Bạn có thể bấm “Gửi lại email kích hoạt” sau.",
            };
        }
    }

    private async Task EnsureRoleAndDepartmentExistAsync(int roleId, int departmentId)
    {
        if (!await _roleRepo.ExistsAsync(roleId))
        {
            throw new ArgumentException($"Vai trò (RoleId={roleId}) không tồn tại.");
        }

        if (await _departmentRepo.GetByIdAsync(departmentId) is null)
        {
            throw new ArgumentException($"Phòng ban (DepartmentId={departmentId}) không tồn tại.");
        }
    }

    private async Task SaveChangesGuardingEmailConflictAsync(string email)
    {
        try
        {
            await _userRepo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race hiếm giống AssetService: 2 request cùng lúc dùng chung Email vượt qua bước kiểm
            // tra trùng ở trên — unique index trên Email là chốt chặn cuối, ánh xạ về 409 thân thiện.
            throw new EmailAlreadyExistsException(email);
        }
    }

    private static UserAuditSnapshot ToAuditSnapshot(User user) => new(
        user.FullName,
        user.Email,
        user.Phone,
        user.RoleId,
        user.DepartmentId,
        user.IsActive);

    private sealed record UserAuditSnapshot(
        string FullName,
        string Email,
        string? Phone,
        int RoleId,
        int DepartmentId,
        bool IsActive);

    private static UserResponseDto MapToDto(User u, ActivationStatusDto? activation = null) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        Phone = u.Phone,
        RoleId = u.RoleId,
        RoleName = u.Role?.Name ?? "",
        DepartmentId = u.DepartmentId,
        DepartmentName = u.Department?.Name ?? "",
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        IsEmployeeAccount = string.Equals(u.Role?.Name, EmployeeRoleName, StringComparison.Ordinal),
        CreatedAt = u.CreatedAt,
        PendingActivation = u.PendingActivation,
        Activation = activation ?? new ActivationStatusDto(),
    };
}
