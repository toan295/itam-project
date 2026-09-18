using ITAM.API.Data;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.Entities;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class AuthService : IAuthService
{
    private const int BCryptWorkFactor = 11;

    // Người tự đăng ký luôn nhận role thấp nhất; nâng role (Manager, Admin IT)
    // phải do Admin thao tác sau, không cho client tự chọn qua /auth/register.
    private const string DefaultRegisterRoleName = "Technician";

    // Message chung, không tiết lộ email có tồn tại trong hệ thống hay không (chống dò email — UC quên mật khẩu).
    private const string ForgotPasswordGenericMessage =
        "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi tới email đó.";

    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly PasswordResetTokenHelper _resetTokenHelper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        JwtHelper jwtHelper,
        PasswordResetTokenHelper resetTokenHelper,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtHelper = jwtHelper;
        _resetTokenHelper = resetTokenHelper;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto)
    {
        var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
        {
            throw new EmailAlreadyExistsException(dto.Email);
        }

        var departmentExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId);
        if (!departmentExists)
        {
            throw new ArgumentException($"DepartmentId '{dto.DepartmentId}' không tồn tại.");
        }

        var defaultRole = await _context.Roles.SingleOrDefaultAsync(r => r.Name == DefaultRegisterRoleName);
        if (defaultRole is null)
        {
            throw new InvalidOperationException(
                $"Role mặc định '{DefaultRegisterRoleName}' chưa tồn tại — cần seed Roles trước khi cho phép đăng ký.");
        }

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, BCryptWorkFactor),
            RoleId = defaultRole.Id,
            DepartmentId = dto.DepartmentId
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} registered successfully", user.Email);

        await _context.Entry(user).Reference(u => u.Role).LoadAsync();
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        // Kiểm tra sau khi xác thực mật khẩu thành công, để không lộ trạng thái tài khoản
        // cho người chưa biết đúng mật khẩu. Thiếu bước này thì tài khoản bị Admin khoá
        // (UC-03) vẫn đăng nhập được bình thường và dùng được mọi API.
        if (!user.IsActive)
        {
            throw new AccountLockedException();
        }

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        return BuildAuthResponse(user);
    }

    public async Task<UserProfileDto?> GetMeAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

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
            IsActive = user.IsActive
        };
    }

    public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        // Không throw/trả lỗi riêng khi không tìm thấy email hoặc tài khoản bị khoá — luôn trả về
        // cùng một message chung để không lộ thông tin "email này có tồn tại trong hệ thống hay không"
        // cho người ngoài dò quét (OWASP: chống User Enumeration).
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation(
                "Forgot-password requested for unknown or inactive email {Email}", dto.Email);
            return new ForgotPasswordResponseDto { Message = ForgotPasswordGenericMessage };
        }

        var token = _resetTokenHelper.GenerateToken(user);
        _logger.LogInformation("Password reset token issued for user {Email}", user.Email);

        // Token luôn được tính ở đây (business logic); Controller sẽ quyết định có trả về client hay
        // không tuỳ môi trường (Development mới trả, Production phải null vì chưa có hạ tầng gửi email).
        return new ForgotPasswordResponseDto
        {
            Message = ForgotPasswordGenericMessage,
            DevOnlyResetToken = token,
        };
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto dto)
    {
        if (!_resetTokenHelper.TryParse(dto.Token, out var payload, out var parseError))
        {
            throw new InvalidResetTokenException(parseError);
        }

        var user = await _context.Users.FindAsync(payload!.UserId);
        if (user is null || !user.IsActive)
        {
            throw new InvalidResetTokenException("Token không hợp lệ.");
        }

        // Fingerprint không khớp nghĩa là mật khẩu đã đổi kể từ khi token này được cấp
        // (đã dùng một token reset khác trước đó, hoặc mật khẩu bị đổi bằng cách khác) -> từ chối.
        if (payload.PasswordFingerprint != PasswordResetTokenHelper.ComputeFingerprint(user.PasswordHash))
        {
            throw new InvalidResetTokenException("Token đã được sử dụng hoặc không còn hiệu lực.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, BCryptWorkFactor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user {Email}", user.Email);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new UserNotFoundException(userId);

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, BCryptWorkFactor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} changed their own password", user.Email);
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
            DepartmentId = user.DepartmentId
        };
    }
}
