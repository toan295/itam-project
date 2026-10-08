using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using ITAM.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ITAM.Tests.Services;

public class AuthServiceTests
{
    private const string Password = "Correct#123";

    private readonly Mock<IUserRepository> _repo = new();

    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<IDefaultPasswordService> _defaultPassword = new();
    private readonly ManualTimeProvider _time = new();
    private readonly LoginAttemptTracker _tracker;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var jwt = new JwtHelper(Options.Create(new JwtSettings
        {
            Secret = "unit-test-secret-key-not-used-in-production-1234567890",
            Issuer = "EAIMS.Tests",
            Audience = "EAIMS.Tests.Client",
            ExpiryMinutes = 60,
        }));
        _tracker = new LoginAttemptTracker(_time);
        _sut = new AuthService(_repo.Object, jwt, _tracker, _defaultPassword.Object, NullLogger<AuthService>.Instance, _audit.Object);
    }

    private static User MakeUser(bool active = true, bool mustChange = false) => new()
    {
        Id = 7,
        FullName = "Nguyen Van A",
        Email = "a@eaims.local",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4),
        RoleId = 1,
        Role = new Role { Id = 1, Name = "Admin IT" },
        DepartmentId = 3,
        IsActive = active,
        MustChangePassword = mustChange,
    };

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokenAndProfile()
    {
        var user = MakeUser(mustChange: true);
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("Admin IT", result.Role);
        Assert.Equal(3, result.DepartmentId);
        Assert.True(result.MustChangePassword);   // frontend dựa vào cờ này để buộc đổi mật khẩu mặc định.
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentials()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsSameExceptionAsWrongPassword()
    {
        // Không được phân biệt "sai email" và "sai mật khẩu" (tránh dò tài khoản).
        _repo.Setup(r => r.GetByEmailWithRoleAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "nobody@eaims.local", Password = Password }));
    }

    [Fact]
    public async Task LoginAsync_LockedAccountWithCorrectPassword_ThrowsAccountLocked()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser(active: false));

        await Assert.ThrowsAsync<AccountLockedException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }));
    }

    [Fact]
    public async Task LoginAsync_LockedAccountWithWrongPassword_DoesNotRevealLockedState()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser(active: false));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = "wrong" }));
    }

    // ---------- Chặn dò mật khẩu theo (IP, email) ----------

    private Task FailLogin(string ip, string email = "a@eaims.local") =>
        Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = email, Password = "wrong" }, ip));

    [Fact]
    public async Task LoginAsync_TooManyFailures_BlocksEvenTheCorrectPassword()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await FailLogin("1.1.1.1");
        }

        var ex = await Assert.ThrowsAsync<TooManyLoginAttemptsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }, "1.1.1.1"));

        Assert.True(ex.RetryAfter > TimeSpan.Zero && ex.RetryAfter <= LoginAttemptTracker.BlockDuration);
    }

    [Fact]
    public async Task LoginAsync_BlockIsPerIpAndEmail_OtherIpAndOtherEmailUnaffected()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync(It.IsAny<string>())).ReturnsAsync(MakeUser());
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await FailLogin("1.1.1.1");
        }

        // Kẻ tấn công ở 1.1.1.1 không khoá được người dùng thật đăng nhập từ IP khác.
        var fromOtherIp = await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }, "2.2.2.2");
        Assert.NotNull(fromOtherIp.Token);

        // Người khác cùng IP (email khác) cũng không bị liên đới.
        var otherEmail = await _sut.LoginAsync(new LoginRequestDto { Email = "b@eaims.local", Password = Password }, "1.1.1.1");
        Assert.NotNull(otherEmail.Token);
    }

    [Fact]
    public async Task LoginAsync_BlockExpiresAfterBlockDuration()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await FailLogin("1.1.1.1");
        }

        _time.Advance(LoginAttemptTracker.BlockDuration + TimeSpan.FromSeconds(1));

        var result = await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }, "1.1.1.1");
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task LoginAsync_SuccessResetsFailureCounter()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());
        for (var i = 0; i < LoginAttemptTracker.MaxFailures - 1; i++)
        {
            await FailLogin("1.1.1.1");
        }

        await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }, "1.1.1.1");
        await FailLogin("1.1.1.1");   // nếu bộ đếm không reset thì lần sai này sẽ kích hoạt chặn.

        var again = await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }, "1.1.1.1");
        Assert.NotNull(again.Token);
    }

    [Fact]
    public async Task LoginAsync_EmailMatchedCaseInsensitively_ForBlocking()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync(It.IsAny<string>())).ReturnsAsync(MakeUser());
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
        {
            await FailLogin("1.1.1.1", i % 2 == 0 ? "A@Eaims.local" : "a@eaims.LOCAL");
        }

        await Assert.ThrowsAsync<TooManyLoginAttemptsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@EAIMS.local", Password = Password }, "1.1.1.1"));
    }

    // ---------- ChangePasswordAsync ----------

    [Fact]
    public async Task ChangePasswordAsync_Valid_UpdatesHashClearsMustChangeAndSaves()
    {
        var user = MakeUser(mustChange: true);
        _repo.Setup(r => r.GetByIdTrackedAsync(7)).ReturnsAsync(user);

        await _sut.ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "Brand#New1" });

        Assert.True(BCrypt.Net.BCrypt.Verify("Brand#New1", user.PasswordHash));
        Assert.False(user.MustChangePassword);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsAndDoesNotSave()
    {
        _repo.Setup(r => r.GetByIdTrackedAsync(7)).ReturnsAsync(MakeUser());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = "nope", NewPassword = "Brand#New1" }));

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_NewEqualsCurrent_ThrowsArgumentException()
    {
        _repo.Setup(r => r.GetByIdTrackedAsync(7)).ReturnsAsync(MakeUser(mustChange: true));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = Password }));

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData("Abc123")]        // dưới 8 ký tự
    [InlineData("abcdefghij")]    // không có chữ số
    [InlineData("1234567890")]    // không có chữ cái
    public async Task ChangePasswordAsync_WeakNewPassword_ThrowsAndDoesNotSave(string weak)
    {
        _repo.Setup(r => r.GetByIdTrackedAsync(7)).ReturnsAsync(MakeUser());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = weak }));

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_NewPasswordIsTheSystemDefault_ThrowsAndDoesNotSave()
    {
        _repo.Setup(r => r.GetByIdTrackedAsync(7)).ReturnsAsync(MakeUser(mustChange: true));
        _defaultPassword.Setup(d => d.IsDefaultPasswordAsync("Default#123")).ReturnsAsync(true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "Default#123" }));

        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ThrowsUserNotFound()
    {
        _repo.Setup(r => r.GetByIdTrackedAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            _sut.ChangePasswordAsync(99, new ChangePasswordRequestDto { CurrentPassword = Password, NewPassword = "Brand#New1" }));
    }

    // ---------- GetMeAsync ----------

    [Fact]
    public async Task GetMeAsync_ExistingUser_ReturnsProfileIncludingMustChangePassword()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(MakeUser(mustChange: true));

        var profile = await _sut.GetMeAsync(7);

        Assert.NotNull(profile);
        Assert.Equal("Admin IT", profile!.Role);
        Assert.True(profile.MustChangePassword);
    }

    [Fact]
    public async Task GetMeAsync_UnknownUser_ReturnsNull()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        Assert.Null(await _sut.GetMeAsync(99));
    }

    // ---------- Nhật ký đăng nhập / đăng xuất ----------

    [Fact]
    public async Task LoginAsync_Success_RecordsLoginAudit()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());

        await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password });

        _audit.Verify(a => a.RecordAsync(7, "Login", "User", 7, null, null), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordForExistingUser_RecordsLoginFailed()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = "sai-mat-khau" }));

        _audit.Verify(a => a.RecordAsync(7, "LoginFailed", "User", 7, null, It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_RecordsNothing()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "x@eaims.local", Password = "abc" }));

        _audit.Verify(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_RecordsLoginFailed()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser(active: false));

        await Assert.ThrowsAsync<AccountLockedException>(() =>
            _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password }));

        _audit.Verify(a => a.RecordAsync(7, "LoginFailed", "User", 7, null, It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_RecordsLogoutAudit()
    {
        await _sut.LogoutAsync(7);

        _audit.Verify(a => a.RecordAsync(7, "Logout", "User", 7, null, null), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenAuditWriteFails_StillLogsIn()
    {
        _repo.Setup(r => r.GetByEmailWithRoleAsync("a@eaims.local")).ReturnsAsync(MakeUser());
        _audit.Setup(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var result = await _sut.LoginAsync(new LoginRequestDto { Email = "a@eaims.local", Password = Password });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }
}
