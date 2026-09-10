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

    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, JwtHelper jwtHelper, ILogger<AuthService> logger)
    {
        _context = context;
        _jwtHelper = jwtHelper;
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
