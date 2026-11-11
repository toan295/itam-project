using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ITAM.API.Configurations;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ITAM.Tests.Services;

public class AccountActivationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAccountActivationRepository> _repo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmployeeRepository> _employeeRepo = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly AccountActivationService _sut;
    private AccountActivation? _added;
    private string? _html;

    public AccountActivationServiceTests()
    {
        var settings = Options.Create(new EmailSettings { FrontendBaseUrl = "http://localhost:5500/", ActivationValidDays = 3 });
        _sut = new AccountActivationService(
            _repo.Object, _userRepo.Object, _employeeRepo.Object, _email.Object, _audit.Object, settings, new Fixed(Now),
            NullLogger<AccountActivationService>.Instance);

        _userRepo.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(MakeUser());
        _employeeRepo.Setup(r => r.GetByUserIdAsync(40)).ReturnsAsync(new Employee { Id = 7, EmployeeCode = "NV0007" });
        _repo.Setup(r => r.GetPendingByUserAsync(40)).ReturnsAsync(new List<AccountActivation>());
        _repo.Setup(r => r.GetLatestByUsersAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, AccountActivation>());
        _repo.Setup(r => r.AddAsync(It.IsAny<AccountActivation>())).Callback<AccountActivation>(a => { a.Id = 1; _added = a; }).Returns(Task.CompletedTask);
        _repo.Setup(r => r.TryActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(true);
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, html, _) => _html = html)
            .ReturnsAsync(new EmailSendResult(true, "smtp"));
    }

    private static User MakeUser(bool pending = true, bool active = true) => new()
    {
        Id = 40, FullName = "Lê Thị Hoa", Email = "nv0007@gmail.com", PendingActivation = pending, IsActive = active,
        RoleId = 4, Role = new Role { Id = 4, Name = "Employee" },
        DepartmentId = 1, Department = new Department { Id = 1, Name = "Phòng <IT>" },
    };

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Token => new('a', 64);

    private static AccountActivation MakeActivation(ActivationStatus status = ActivationStatus.Pending, DateTime? expires = null, User? user = null) => new()
    {
        Id = 1, UserId = 40, User = user ?? MakeUser(), TokenHash = Sha256(Token), EmailTo = "nv0007@gmail.com", Reason = "Created",
        Status = status, CreatedAt = Now.AddHours(-1), ExpiresAt = expires ?? Now.AddDays(2),
    };

    private void Found(AccountActivation a) => _repo.Setup(r => r.GetByTokenHashAsync(Sha256(Token))).ReturnsAsync(a);

    // ----- gửi email -----

    [Fact]
    public async Task Send_CreatesHashedTokenWith3DayExpiry_AndEmailHasEveryRequiredSection()
    {
        var result = await _sut.SendAsync(40, IAccountActivationService.ReasonCreated);

        Assert.True(result.Sent);
        Assert.Equal(Now.AddDays(3), _added!.ExpiresAt);
        var token = Regex.Match(_html!, @"token=([0-9a-f]{64})").Groups[1].Value;
        Assert.Equal(64, token.Length);
        Assert.Equal(Sha256(token), _added.TokenHash);            // chỉ lưu băm
        Assert.DoesNotContain(token, _added.TokenHash);
        Assert.Contains("activate-account.html?token=", _html);
        Assert.Contains("Kích hoạt tài khoản", _html);             // tiêu đề + nút
        Assert.Contains("Xin chào", _html);                         // lời chào
        Assert.Contains("NV0007", _html);                           // thông tin tài khoản
        Assert.Contains("nv0007@gmail.com", _html);
        Assert.Contains("Nhân viên", _html);
        Assert.Contains("Hướng dẫn", _html);                        // hướng dẫn
        Assert.Contains("Lưu ý bảo mật", _html);                    // lưu ý bảo mật
        Assert.Contains("Phòng &lt;IT&gt;", _html);                 // dữ liệu người dùng được HTML-encode
        Assert.DoesNotContain("Phòng <IT>", _html);
    }

    [Fact]
    public async Task Send_SupersedesOlderPendingLinks()
    {
        var old = MakeActivation();
        _repo.Setup(r => r.GetPendingByUserAsync(40)).ReturnsAsync(new List<AccountActivation> { old });

        await _sut.SendAsync(40, IAccountActivationService.ReasonResent);

        Assert.Equal(ActivationStatus.Superseded, old.Status);
    }

    [Fact]
    public async Task Send_ResentWithin60Seconds_IsRejected()
    {
        var recent = MakeActivation();
        recent.CreatedAt = Now.AddSeconds(-20);
        _repo.Setup(r => r.GetLatestByUsersAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, AccountActivation> { [40] = recent });

        await Assert.ThrowsAsync<ActivationResendTooSoonException>(() => _sut.SendAsync(40, IAccountActivationService.ReasonResent));
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Send_AccountAlreadyActivated_IsRejected()
    {
        _userRepo.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(MakeUser(pending: false));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SendAsync(40, IAccountActivationService.ReasonCreated));
    }

    [Fact]
    public async Task Send_SmtpFails_RecordsErrorAndReportsNotSent()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult(false, "smtp", "Mailbox unavailable"));

        var result = await _sut.SendAsync(40, IAccountActivationService.ReasonCreated);

        Assert.False(result.Sent);
        Assert.Equal("Mailbox unavailable", _added!.SendError);
        Assert.Null(_added.SentAt);
    }

    // ----- trang công khai -----

    [Fact]
    public async Task GetPublicInfo_ReturnsAccountSummaryAndPendingStatus()
    {
        Found(MakeActivation());

        var info = await _sut.GetPublicInfoAsync(Token);

        Assert.Equal("Pending", info.Status);
        Assert.Equal("Lê Thị Hoa", info.FullName);
        Assert.Equal("NV0007", info.EmployeeCode);
        Assert.Equal("Nhân viên", info.RoleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzz")]
    public async Task GetPublicInfo_MalformedToken_IsInvalid(string token) =>
        await Assert.ThrowsAsync<ActivationLinkInvalidException>(() => _sut.GetPublicInfoAsync(token));

    [Fact]
    public async Task GetPublicInfo_UnknownToken_IsInvalid() =>
        await Assert.ThrowsAsync<ActivationLinkInvalidException>(() => _sut.GetPublicInfoAsync(Token));

    // ----- kích hoạt -----

    [Fact]
    public async Task Activate_Valid_SetsHashedPasswordOfTheirOwn_AndAudits()
    {
        Found(MakeActivation());
        string? hash = null;
        _repo.Setup(r => r.TryActivateAsync(1, 40, It.IsAny<string>(), Now)).Callback<int, int, string, DateTime>((_, _, h, _) => hash = h).ReturnsAsync(true);

        var info = await _sut.ActivateAsync(Token, "MatKhau@2026", "MatKhau@2026");

        Assert.Equal("Activated", info.Status);
        Assert.True(BCrypt.Net.BCrypt.Verify("MatKhau@2026", hash));
        Assert.NotEqual("MatKhau@2026", hash);
        _audit.Verify(a => a.RecordAsync(40, "ActivateAccount", "User", 40, It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Theory]
    [InlineData("short1", "short1")]                 // dưới 8 ký tự
    [InlineData("MatKhau@2026", "MatKhau@2027")]     // nhập lại không khớp
    public async Task Activate_BadPassword_IsRejectedBeforeAnythingIsConsumed(string password, string confirm)
    {
        Found(MakeActivation());

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ActivateAsync(Token, password, confirm));
        _repo.Verify(r => r.TryActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Theory]
    [InlineData("khongcoso")]      // thiếu số
    [InlineData("12345678")]       // thiếu chữ
    public async Task Activate_PasswordWithoutBothLettersAndDigits_IsRejected(string password)
    {
        Found(MakeActivation());

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ActivateAsync(Token, password, password));
        _repo.Verify(r => r.TryActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Send_ResetReason_UsesResetWording_AndIsNotRateLimited()
    {
        var recent = MakeActivation();
        recent.CreatedAt = Now.AddSeconds(-5);
        _repo.Setup(r => r.GetLatestByUsersAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, AccountActivation> { [40] = recent });

        var result = await _sut.SendAsync(40, IAccountActivationService.ReasonReset);

        Assert.True(result.Sent);
        Assert.Contains("Đặt lại mật khẩu", _html);
        Assert.Contains("Đặt mật khẩu mới", _html);
        Assert.Equal("Reset", _added!.Reason);
    }

    [Fact]
    public async Task Activate_PasswordLongerThan72_IsRejected()
    {
        Found(MakeActivation());
        var tooLong = new string('x', 73);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ActivateAsync(Token, tooLong, tooLong));
    }

    [Theory]
    [InlineData(ActivationStatus.Activated)]
    [InlineData(ActivationStatus.Superseded)]
    public async Task Activate_UsedOrSupersededLink_IsUnusable(ActivationStatus status)
    {
        Found(MakeActivation(status));

        await Assert.ThrowsAsync<ActivationLinkUnusableException>(() => _sut.ActivateAsync(Token, "MatKhau@2026", "MatKhau@2026"));
        _repo.Verify(r => r.TryActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Activate_ExpiredLink_IsUnusable()
    {
        Found(MakeActivation(expires: Now.AddMinutes(-1)));

        await Assert.ThrowsAsync<ActivationLinkUnusableException>(() => _sut.ActivateAsync(Token, "MatKhau@2026", "MatKhau@2026"));
    }

    [Fact]
    public async Task Activate_LockedAccount_IsUnusable()
    {
        Found(MakeActivation(user: MakeUser(active: false)));

        await Assert.ThrowsAsync<ActivationLinkUnusableException>(() => _sut.ActivateAsync(Token, "MatKhau@2026", "MatKhau@2026"));
    }

    [Fact]
    public async Task Activate_LosesTheRace_IsUnusable()
    {
        Found(MakeActivation());
        _repo.Setup(r => r.TryActivateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ActivationLinkUnusableException>(() => _sut.ActivateAsync(Token, "MatKhau@2026", "MatKhau@2026"));
        _audit.Verify(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
    }

    // ----- trạng thái -----

    [Fact]
    public async Task GetStatuses_ComputesExpiredAndNone()
    {
        var expired = MakeActivation(expires: Now.AddHours(-2));
        _repo.Setup(r => r.GetLatestByUsersAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, AccountActivation> { [40] = expired });

        var map = await _sut.GetStatusesAsync(new[] { 40, 41 });

        Assert.Equal("Expired", map[40].Status);
        Assert.Equal("None", map[41].Status);
    }

    [Fact]
    public async Task InvalidatePending_MarksPendingLinksSuperseded()
    {
        var pending = MakeActivation();
        _repo.Setup(r => r.GetPendingByUserAsync(40)).ReturnsAsync(new List<AccountActivation> { pending });

        await _sut.InvalidatePendingAsync(40);

        Assert.Equal(ActivationStatus.Superseded, pending.Status);
    }

    private sealed class Fixed : TimeProvider
    {
        private readonly DateTime _now;
        public Fixed(DateTime now) => _now = now;
        public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);
    }
}
