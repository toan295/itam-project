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
    // Số kỹ thuật viên đang hoạt động khuyến nghị tối thiểu — họ phục vụ tất cả phòng ban. Chỉ cảnh báo, không chặn.
    private const int MinActiveTechnicians = 3;

    private const int BCryptWorkFactor = 11;

    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly IDefaultPasswordService _defaultPasswordService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepo,
        IRoleRepository roleRepo,
        IDepartmentRepository departmentRepo,
        IDefaultPasswordService defaultPasswordService,
        IAuditLogService auditLogService,
        ILogger<UserService> logger)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _departmentRepo = departmentRepo;
        _defaultPasswordService = defaultPasswordService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PagedResultDto<UserResponseDto>> GetPagedAsync(int page, int pageSize, string? search)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, total) = await _userRepo.GetPagedAsync(page, pageSize, search);
        return new PagedResultDto<UserResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<UserResponseDto> GetByIdAsync(int id)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        return MapToDto(user);
    }

    public async Task<UserResponseDto> CreateAsync(CreateUserRequestDto dto, int currentUserId)
    {
        var email = dto.Email.Trim();

        var existed = await _userRepo.GetByEmailAsync(email);
        if (existed is not null)
        {
            throw new EmailAlreadyExistsException(email);
        }

        await EnsureRoleAndDepartmentExistAsync(dto.RoleId, dto.DepartmentId);
        var passwordHash = await HashDefaultPasswordAsync();

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            // Tài khoản mới dùng mật khẩu mặc định do Admin IT cấu hình; buộc đổi ở lần đăng nhập đầu.
            PasswordHash = passwordHash,
            MustChangePassword = true,
            RoleId = dto.RoleId,
            DepartmentId = dto.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _userRepo.AddAsync(user);
        await SaveChangesGuardingEmailConflictAsync(email);

        var created = await _userRepo.GetByIdWithDetailsAsync(user.Id) ?? throw new UserNotFoundException(user.Id);

        _logger.LogInformation("User {Email} created by Admin IT with the default password", created.Email);

        // Lần lưu thứ 2 (bắt buộc): EntityId của log là Id tự sinh, chỉ có sau lần lưu User ở trên.
        await _auditLogService.RecordAsync(
            currentUserId,
            "Create",
            "User",
            created.Id,
            oldValue: null,
            newValue: ToAuditSnapshot(created));
        await _userRepo.SaveChangesAsync();

        return MapToDto(created);
    }

    public async Task<UserResponseDto> UpdateAsync(int id, UpdateUserRequestDto dto, int currentUserId)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        var oldValue = ToAuditSnapshot(user);

        // UC-03 quy tắc nghiệp vụ: không được tự khoá tài khoản của chính mình.
        if (id == currentUserId && !dto.IsActive)
        {
            throw new SelfLockNotAllowedException();
        }

        await EnsureRoleAndDepartmentExistAsync(dto.RoleId, dto.DepartmentId);

        var email = dto.Email.Trim();
        var emailChanged = !string.Equals(email, user.Email, StringComparison.Ordinal);
        if (emailChanged)
        {
            var existed = await _userRepo.GetByEmailAsync(email);
            if (existed is not null)
            {
                throw new EmailAlreadyExistsException(email);
            }
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

        user.FullName = dto.FullName.Trim();
        user.Email = email;
        user.RoleId = dto.RoleId;
        user.DepartmentId = dto.DepartmentId;
        user.IsActive = dto.IsActive;

        _userRepo.Update(user);

        // Id đã biết trước -> ghi log rồi lưu MỘT lần: User và AuditLog cùng commit (atomic).
        await _auditLogService.RecordAsync(
            currentUserId,
            "Update",
            "User",
            user.Id,
            oldValue,
            ToAuditSnapshot(user));
        await SaveChangesGuardingEmailConflictAsync(email);

        var updated = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        var result = MapToDto(updated);
        result.Warning = warning;
        return result;
    }

    public async Task<UserResponseDto> ResetPasswordAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new SelfPasswordResetNotAllowedException();
        }

        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);

        user.PasswordHash = await HashDefaultPasswordAsync();
        user.MustChangePassword = true;
        _userRepo.Update(user);
        _logger.LogInformation("Password reset to default by Admin IT for user {Email}", user.Email);

        await _auditLogService.RecordAsync(
            currentUserId,
            "ResetPassword",
            "User",
            user.Id,
            oldValue: null,
            newValue: new { PasswordResetToDefault = true });
        await _userRepo.SaveChangesAsync();

        var updated = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);
        return MapToDto(updated);
    }

    private async Task<string> HashDefaultPasswordAsync() =>
        BCrypt.Net.BCrypt.HashPassword(await _defaultPasswordService.GetRequiredAsync(), BCryptWorkFactor);

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
        user.RoleId,
        user.DepartmentId,
        user.IsActive);

    private sealed record UserAuditSnapshot(
        string FullName,
        string Email,
        int RoleId,
        int DepartmentId,
        bool IsActive);

    private static UserResponseDto MapToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        RoleId = u.RoleId,
        RoleName = u.Role?.Name ?? "",
        DepartmentId = u.DepartmentId,
        DepartmentName = u.Department?.Name ?? "",
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
    };
}
