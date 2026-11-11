using System.Security.Cryptography;
using System.Text;
using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ITAM.API.Services.Implementations;

public class AccountActivationService : IAccountActivationService
{
    private const int ResendCooldownSeconds = 60;
    private const int SendErrorMaxLength = 500;
    private const int TokenBytes = 32;   // 256 bit ngẫu nhiên -> không đoán/duyệt vét được
    private const int BCryptWorkFactor = 11;
    private const string EmployeeRoleName = "Employee";

    private readonly IAccountActivationRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IEmailSender _email;
    private readonly IAuditLogService _audit;
    private readonly EmailSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<AccountActivationService> _logger;

    public AccountActivationService(
        IAccountActivationRepository repo,
        IUserRepository userRepo,
        IEmployeeRepository employeeRepo,
        IEmailSender email,
        IAuditLogService audit,
        IOptions<EmailSettings> settings,
        TimeProvider time,
        ILogger<AccountActivationService> logger)
    {
        _repo = repo;
        _userRepo = userRepo;
        _employeeRepo = employeeRepo;
        _email = email;
        _audit = audit;
        _settings = settings.Value;
        _time = time;
        _logger = logger;
    }

    // ---------- Gửi email kích hoạt ----------

    public async Task<EmailDispatchDto> SendAsync(int userId, string reason)
    {
        var user = await _userRepo.GetByIdWithDetailsAsync(userId) ?? throw new UserNotFoundException(userId);
        if (!user.PendingActivation)
        {
            throw new ArgumentException("Tài khoản này đã được kích hoạt hoặc không cần kích hoạt.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return new EmailDispatchDto
            {
                Sent = false, Mode = "disabled",
                Message = "Chưa có địa chỉ email nên không gửi được email kích hoạt. Hãy bổ sung email của nhân viên rồi gửi lại.",
            };
        }

        var now = Now();
        if (reason == IAccountActivationService.ReasonResent)
        {
            var latest = (await _repo.GetLatestByUsersAsync(new[] { userId })).GetValueOrDefault(userId);
            if (latest is not null)
            {
                var wait = ResendCooldownSeconds - (int)(now - latest.CreatedAt).TotalSeconds;
                if (wait > 0)
                {
                    throw new ActivationResendTooSoonException(wait);
                }
            }
        }

        // Liên kết cũ còn Pending bị vô hiệu: tại mọi thời điểm chỉ một email có hiệu lực.
        foreach (var old in await _repo.GetPendingByUserAsync(userId))
        {
            old.Status = ActivationStatus.Superseded;
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();
        var activation = new AccountActivation
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            EmailTo = user.Email,
            Reason = reason,
            Status = ActivationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_settings.ActivationValidDays),
        };
        await _repo.AddAsync(activation);
        await _repo.SaveChangesAsync();

        EmailSendResult result;
        try
        {
            var employee = await _employeeRepo.GetByUserIdAsync(userId);
            var content = AccountActivationEmail.Build(
                user.FullName, employee?.EmployeeCode, user.Email, user.Phone ?? employee?.Phone, RoleLabel(user.Role?.Name),
                user.Department?.Name ?? "", BuildActivateUrl(rawToken), activation.ExpiresAt,
                _settings.ActivationValidDays, reason,
                isEmployee: string.Equals(user.Role?.Name, EmployeeRoleName, StringComparison.Ordinal));
            result = await _email.SendAsync(user.Email, content.Subject, content.HtmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong đợi khi gửi email kích hoạt cho user {UserId}", userId);
            result = new EmailSendResult(false, "smtp", $"Lỗi khi gửi email: {ex.Message}");
        }

        activation.SentAt = result.Sent ? now : null;
        activation.SendError = result.Sent ? null : Truncate(result.Detail ?? "Không gửi được email.", SendErrorMaxLength);
        await _repo.SaveChangesAsync();

        return new EmailDispatchDto
        {
            Sent = result.Sent,
            Mode = result.Mode,
            Message = Describe(result, user.Email),
        };
    }

    public async Task<Dictionary<int, ActivationStatusDto>> GetStatusesAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        var latest = await _repo.GetLatestByUsersAsync(ids);
        var now = Now();
        return ids.ToDictionary(id => id, id => latest.TryGetValue(id, out var a) ? ToStatusDto(a, now) : new ActivationStatusDto());
    }

    public async Task InvalidatePendingAsync(int userId)
    {
        foreach (var old in await _repo.GetPendingByUserAsync(userId))
        {
            old.Status = ActivationStatus.Superseded;
        }
    }

    // ---------- Luồng công khai ----------

    public async Task<ActivationPublicInfoDto> GetPublicInfoAsync(string token)
    {
        var activation = await FindAsync(token);
        return await ToPublicInfoAsync(activation, Now());
    }

    public async Task<ActivationPublicInfoDto> ActivateAsync(string token, string password, string confirmPassword)
    {
        // Cùng chính sách với đổi mật khẩu: 8–72 ký tự, gồm cả chữ và số.
        var policyError = PasswordPolicy.Validate(password);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw new ArgumentException("Mật khẩu nhập lại không khớp.");
        }

        var activation = await FindAsync(token);
        var now = Now();
        EnsureUsable(activation, now);

        var hash = BCrypt.Net.BCrypt.HashPassword(password, BCryptWorkFactor);
        if (!await _repo.TryActivateAsync(activation.Id, activation.UserId, hash, now))
        {
            throw new ActivationLinkUnusableException("Liên kết không còn dùng được (đã được dùng hoặc vừa hết hạn). Hãy nhờ Admin IT gửi lại email kích hoạt.");
        }

        await _audit.RecordAsync(activation.UserId, "ActivateAccount", "User", activation.UserId,
            oldValue: null, newValue: new { activation.User.Email });
        await _repo.SaveChangesAsync();
        _logger.LogInformation("User {Email} đã kích hoạt tài khoản", activation.User.Email);

        activation.Status = ActivationStatus.Activated;
        return await ToPublicInfoAsync(activation, now);
    }

    // ---------- helpers ----------

    private async Task<AccountActivation> FindAsync(string token)
    {
        // Token hợp lệ luôn là 64 ký tự hex — chặn sớm rác để khỏi truy vấn DB.
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenBytes * 2)
        {
            throw new ActivationLinkInvalidException();
        }

        return await _repo.GetByTokenHashAsync(HashToken(token)) ?? throw new ActivationLinkInvalidException();
    }

    private static void EnsureUsable(AccountActivation a, DateTime now)
    {
        switch (a.Status)
        {
            case ActivationStatus.Activated:
                throw new ActivationLinkUnusableException("Tài khoản đã được kích hoạt. Bạn có thể đăng nhập bằng mật khẩu đã đặt.");
            case ActivationStatus.Superseded:
                throw new ActivationLinkUnusableException("Liên kết này đã bị thay bằng email kích hoạt mới hơn. Hãy dùng liên kết trong email mới nhất.");
        }

        if (a.ExpiresAt <= now)
        {
            throw new ActivationLinkUnusableException("Liên kết đã hết hạn. Hãy nhờ Admin IT gửi lại email kích hoạt.");
        }

        if (!a.User.PendingActivation || !a.User.IsActive)
        {
            throw new ActivationLinkUnusableException("Tài khoản không thể kích hoạt lúc này. Vui lòng liên hệ Admin IT.");
        }
    }

    private async Task<ActivationPublicInfoDto> ToPublicInfoAsync(AccountActivation a, DateTime now)
    {
        var employee = await _employeeRepo.GetByUserIdAsync(a.UserId);
        return new ActivationPublicInfoDto
        {
            FullName = a.User.FullName,
            EmployeeCode = employee?.EmployeeCode,
            Email = a.User.Email,
            RoleName = RoleLabel(a.User.Role?.Name),
            DepartmentName = a.User.Department?.Name ?? "",
            Status = ComputeStatus(a, now),
            ExpiresAt = a.ExpiresAt,
            Purpose = a.Reason == IAccountActivationService.ReasonReset ? "Reset" : "Activate",
        };
    }

    private static string RoleLabel(string? roleName) =>
        string.Equals(roleName, EmployeeRoleName, StringComparison.Ordinal) ? "Nhân viên" : roleName ?? "";

    private static string ComputeStatus(AccountActivation a, DateTime now) => a.Status switch
    {
        ActivationStatus.Pending when a.ExpiresAt <= now => "Expired",
        ActivationStatus.Pending => "Pending",
        ActivationStatus.Activated => "Activated",
        _ => "Superseded",
    };

    private static ActivationStatusDto ToStatusDto(AccountActivation a, DateTime now) => new()
    {
        // Superseded không bao giờ là lần mới nhất của một tài khoản còn chờ; nếu gặp thì coi như đang chờ.
        Status = a.Status == ActivationStatus.Superseded ? "Pending" : ComputeStatus(a, now),
        EmailTo = a.EmailTo,
        SentAt = a.SentAt,
        ExpiresAt = a.ExpiresAt,
        ActivatedAt = a.ActivatedAt,
        SendError = a.SendError,
    };

    private string BuildActivateUrl(string rawToken) =>
        $"{_settings.FrontendBaseUrl.TrimEnd('/')}/pages/activate-account.html?token={rawToken}";

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;

    internal static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static string Describe(EmailSendResult result, string address) => result.Mode switch
    {
        "smtp" when result.Sent => $"Đã gửi email kích hoạt tới {address}.",
        "outbox" => result.Detail ?? "Email được lưu thử, chưa gửi tới hộp thư thật.",
        _ => $"Không gửi được email kích hoạt tới {address}: {result.Detail ?? "chưa cấu hình email"}. Bạn có thể bấm “Gửi lại email kích hoạt” sau khi khắc phục.",
    };
}
