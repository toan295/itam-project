using ITAM.API.Helpers;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class AuthService : IAuthService
{
    private const int BCryptWorkFactor = 11;

    // Hash BCrypt hợp lệ (cùng work factor) của một chuỗi ngẫu nhiên, dùng để cân bằng thời gian khi không có user.
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), BCryptWorkFactor);

    private readonly IUserRepository _userRepository;
    private readonly JwtHelper _jwtHelper;
    private readonly ILoginAttemptTracker _loginAttempts;
    private readonly IDefaultPasswordService _defaultPassword;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditLogService _auditLog;

    public AuthService(
        IUserRepository userRepository,
        JwtHelper jwtHelper,
        ILoginAttemptTracker loginAttempts,
        IDefaultPasswordService defaultPassword,
        ILogger<AuthService> logger,
        IAuditLogService auditLog)
    {
        _userRepository = userRepository;
        _jwtHelper = jwtHelper;
        _loginAttempts = loginAttempts;
        _defaultPassword = defaultPassword;
        _logger = logger;
        _auditLog = auditLog;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string? clientIp = null)
    {
        var attemptKey = $"{clientIp ?? "unknown"}|{dto.Email.Trim().ToLowerInvariant()}";
        if (_loginAttempts.IsBlocked(attemptKey, out var retryAfter))
        {
            throw new TooManyLoginAttemptsException(retryAfter);
        }

        var user = await _userRepository.GetByEmailWithRoleAsync(dto.Email);

        // Luôn chạy một lần BCrypt dù email không tồn tại — nếu không, request cho email không có thật trả về
        // nhanh hơn hẳn (không băm), cho phép dò email nào có tài khoản qua thời gian phản hồi.
        var passwordOk = BCrypt.Net.BCrypt.Verify(dto.Password, user?.PasswordHash ?? DummyPasswordHash);
        if (user is null || !passwordOk)
        {
            _loginAttempts.RecordFailure(attemptKey);
            if (user is not null)
            {
                // Chỉ ghi được khi email thuộc một tài khoản có thật (nhật ký cần UserId) — dấu vết đoán mật khẩu.
                await RecordAsync(user.Id, "LoginFailed", new { reason = "WrongPassword" });
            }

            throw new InvalidCredentialsException();
        }

        _loginAttempts.Reset(attemptKey);

        // Kiểm tra sau khi xác thực mật khẩu thành công, để không lộ trạng thái tài khoản
        // cho người chưa biết đúng mật khẩu. Thiếu bước này thì tài khoản bị Admin khoá
        // (UC-03) vẫn đăng nhập được bình thường và dùng được mọi API.
        if (!user.IsActive)
        {
            await RecordAsync(user.Id, "LoginFailed", new { reason = "AccountLocked" });
            throw new AccountLockedException();
        }

        await RecordAsync(user.Id, "Login", null);
        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        return BuildAuthResponse(user);
    }

    public async Task<UserProfileDto?> GetMeAsync(int userId)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(userId);

        if (user is null)
        {
            return null;
        }

        return new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.Name,
            DepartmentId = user.DepartmentId,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword
        };
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto dto)
    {
        var user = await _userRepository.GetByIdTrackedAsync(userId)
            ?? throw new UserNotFoundException(userId);

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (dto.NewPassword == dto.CurrentPassword)
        {
            throw new ArgumentException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        var policyError = PasswordPolicy.Validate(dto.NewPassword);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError);
        }

        // Không cho "đổi" sang lại chính mật khẩu mặc định (mọi tài khoản mới đều biết giá trị này).
        if (await _defaultPassword.IsDefaultPasswordAsync(dto.NewPassword))
        {
            throw new ArgumentException("Mật khẩu mới không được trùng mật khẩu mặc định của hệ thống.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, BCryptWorkFactor);
        user.MustChangePassword = false;
        await _auditLog.RecordAsync(user.Id, "ChangePassword", "User", user.Id);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User {Email} changed their own password", user.Email);
    }

    // JWT không có phía server để huỷ; endpoint này chỉ để ghi nhận hành động đăng xuất vào nhật ký.
    public async Task LogoutAsync(int userId) => await RecordAsync(userId, "Logout", null);

    // Nhật ký không được làm hỏng đăng nhập/đăng xuất: lỗi ghi log chỉ cảnh báo.
    private async Task RecordAsync(int userId, string action, object? newValue)
    {
        try
        {
            await _auditLog.RecordAsync(userId, action, "User", userId, null, newValue);
            await _userRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký {Action} cho UserId={UserId}", action, userId);
        }
    }

    private AuthResponseDto BuildAuthResponse(User user)
    {
        var (token, expiresAt) = _jwtHelper.GenerateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.Name,
            DepartmentId = user.DepartmentId,
            MustChangePassword = user.MustChangePassword
        };
    }
}
