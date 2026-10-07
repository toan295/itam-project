using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class UserService : IUserService
{
    private const string AdminRoleName = "Admin IT";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    // Link do Admin IT cấp (tạo tài khoản/đặt lại mật khẩu hộ) sống lâu hơn token tự phục vụ
    // (15 phút) vì Admin có thể chưa chuyển ngay cho người dùng cuối.
    private static readonly TimeSpan AdminIssuedLinkLifetime = TimeSpan.FromHours(24);

    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IDepartmentRepository _departmentRepo;
    private readonly PasswordResetTokenHelper _resetTokenHelper;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepo,
        IRoleRepository roleRepo,
        IDepartmentRepository departmentRepo,
        PasswordResetTokenHelper resetTokenHelper,
        IAuditLogService auditLogService,
        ILogger<UserService> logger)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _departmentRepo = departmentRepo;
        _resetTokenHelper = resetTokenHelper;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PagedResultDto<UserResponseDto>> GetPagedAsync(int page, int pageSize, string? search)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

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

    public async Task<UserSetupLinkResponseDto> CreateAsync(CreateUserRequestDto dto, int currentUserId)
    {
        var email = dto.Email.Trim();

        var existed = await _userRepo.GetByEmailAsync(email);
        if (existed is not null)
        {
            throw new EmailAlreadyExistsException(email);
        }

        await EnsureRoleAndDepartmentExistAsync(dto.RoleId, dto.DepartmentId);

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            // Mật khẩu ngẫu nhiên, không ai (kể cả Admin IT vừa tạo) biết được — tài khoản chỉ
            // dùng được sau khi người dùng tự đặt mật khẩu qua link thiết lập bên dưới.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid(), 11),
            RoleId = dto.RoleId,
            DepartmentId = dto.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _userRepo.AddAsync(user);
        await SaveChangesGuardingEmailConflictAsync(email);

        var created = await _userRepo.GetByIdWithDetailsAsync(user.Id) ?? throw new UserNotFoundException(user.Id);
        var setupToken = _resetTokenHelper.GenerateToken(created, AdminIssuedLinkLifetime);

        _logger.LogInformation("User {Email} created by Admin IT, setup link issued", created.Email);

        await _auditLogService.RecordAsync(
            currentUserId,
            "Create",
            "User",
            created.Id,
            oldValue: null,
            newValue: ToAuditSnapshot(created));

        return new UserSetupLinkResponseDto { User = MapToDto(created), DevOnlySetupToken = setupToken };
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

        user.FullName = dto.FullName.Trim();
        user.Email = email;
        user.RoleId = dto.RoleId;
        user.DepartmentId = dto.DepartmentId;
        user.IsActive = dto.IsActive;

        _userRepo.Update(user);
        await SaveChangesGuardingEmailConflictAsync(email);

        var updated = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);

        await _auditLogService.RecordAsync(
            currentUserId,
            "Update",
            "User",
            updated.Id,
            oldValue,
            ToAuditSnapshot(updated));

        return MapToDto(updated);
    }

    public async Task<UserSetupLinkResponseDto> ResetPasswordAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new SelfPasswordResetNotAllowedException();
        }

        var user = await _userRepo.GetByIdWithDetailsAsync(id) ?? throw new UserNotFoundException(id);

        var token = _resetTokenHelper.GenerateToken(user, AdminIssuedLinkLifetime);
        _logger.LogInformation("Password reset link issued by Admin IT for user {Email}", user.Email);

        await _auditLogService.RecordAsync(
            currentUserId,
            "ResetPassword",
            "User",
            user.Id,
            oldValue: null,
            newValue: new { ResetLinkIssued = true });

        return new UserSetupLinkResponseDto { User = MapToDto(user), DevOnlySetupToken = token };
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
        CreatedAt = u.CreatedAt,
    };
}
