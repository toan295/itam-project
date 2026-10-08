using ITAM.API.Helpers;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class DefaultPasswordServiceTests
{
    private readonly Mock<ISystemSettingRepository> _repoMock = new();
    private readonly Mock<IAuditLogService> _auditMock = new();
    private readonly DefaultPasswordCipher _cipher = new(new EphemeralDataProtectionProvider());
    private readonly DefaultPasswordService _sut;

    public DefaultPasswordServiceTests()
    {
        _sut = new DefaultPasswordService(_repoMock.Object, _auditMock.Object, _cipher, NullLogger<DefaultPasswordService>.Instance);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<SystemSetting>()))
            .Callback<SystemSetting>(s => s.Id = 1)
            .Returns(Task.CompletedTask);
    }

    private SystemSetting Stored(string plain, int id = 4) =>
        new() { Id = id, Key = "DefaultPassword", Value = _cipher.Protect(plain) };

    [Fact]
    public async Task GetAsync_NotConfigured_ReturnsIsConfiguredFalse()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync((SystemSetting?)null);

        var result = await _sut.GetAsync();

        Assert.False(result.IsConfigured);
        Assert.Null(result.Password);
    }

    [Fact]
    public async Task GetAsync_EncryptedValue_ReturnsDecryptedPassword()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync(Stored("Secret@123"));

        var result = await _sut.GetAsync();

        Assert.True(result.IsConfigured);
        Assert.Equal("Secret@123", result.Password);
    }

    [Fact]
    public async Task GetAsync_LegacyPlaintextValue_StillReadable()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword"))
            .ReturnsAsync(new SystemSetting { Id = 4, Key = "DefaultPassword", Value = "Legacy@123" });

        Assert.Equal("Legacy@123", (await _sut.GetAsync()).Password);
    }

    [Fact]
    public async Task GetAsync_KeyLost_TreatedAsNotConfiguredInsteadOfThrowing()
    {
        // Giá trị được mã hoá bằng một khoá khác (khoá cũ đã mất) -> không giải mã được.
        var otherKeyRing = new DefaultPasswordCipher(new EphemeralDataProtectionProvider());
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword"))
            .ReturnsAsync(new SystemSetting { Id = 4, Key = "DefaultPassword", Value = otherKeyRing.Protect("Secret@123") });

        var result = await _sut.GetAsync();

        Assert.False(result.IsConfigured);
        Assert.Null(result.Password);
        await Assert.ThrowsAsync<DefaultPasswordNotConfiguredException>(() => _sut.GetRequiredAsync());
    }

    [Fact]
    public async Task SetAsync_FirstTime_StoresEncryptedAndAuditsWithoutPassword()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync((SystemSetting?)null);
        object? audited = null;
        _auditMock.Setup(a => a.RecordAsync(7, "Create", "DefaultPassword", 1, It.IsAny<object?>(), It.IsAny<object?>()))
            .Callback<int, string, string, int, object?, object?>((_, _, _, _, o, n) => audited = (o, n))
            .Returns(Task.CompletedTask);

        var result = await _sut.SetAsync("Secret@123", currentUserId: 7);

        Assert.True(result.IsConfigured);
        Assert.Equal("Secret@123", result.Password);
        Assert.DoesNotContain("Secret@123", audited!.ToString()); // audit log không được chứa mật khẩu.
        _repoMock.Verify(r => r.AddAsync(It.Is<SystemSetting>(s =>
            DefaultPasswordCipher.IsProtected(s.Value) && !s.Value.Contains("Secret@123"))), Times.Once);
    }

    [Fact]
    public async Task SetAsync_Existing_UpdatesWithEncryptedValue()
    {
        var existing = Stored("Old@12345");
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync(existing);

        await _sut.SetAsync("New@12345", currentUserId: 7);

        Assert.True(DefaultPasswordCipher.IsProtected(existing.Value));
        Assert.Equal("New@12345", _cipher.Unprotect(existing.Value));
        _repoMock.Verify(r => r.AddAsync(It.IsAny<SystemSetting>()), Times.Never);
        _auditMock.Verify(a => a.RecordAsync(7, "Update", "DefaultPassword", 4, It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Theory]
    [InlineData("Abc123")]          // quá ngắn (< 8)
    [InlineData("abcdefgh")]        // không có chữ số
    [InlineData("12345678")]        // không có chữ cái
    [InlineData(" Abcdef12")]       // khoảng trắng đầu
    public async Task SetAsync_PolicyViolation_ThrowsAndDoesNotSave(string password)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SetAsync(password, currentUserId: 7));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ClearAsync_Existing_RemovesSetting()
    {
        var existing = Stored("Old@12345");
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync(existing);

        await _sut.ClearAsync(currentUserId: 7);

        _repoMock.Verify(r => r.Remove(existing), Times.Once);
        _auditMock.Verify(a => a.RecordAsync(7, "Delete", "DefaultPassword", 4, It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task ClearAsync_NotConfigured_DoesNothing()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync((SystemSetting?)null);

        await _sut.ClearAsync(currentUserId: 7);

        _repoMock.Verify(r => r.Remove(It.IsAny<SystemSetting>()), Times.Never);
    }

    [Fact]
    public async Task GetRequiredAsync_NotConfigured_Throws()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync((SystemSetting?)null);

        await Assert.ThrowsAsync<DefaultPasswordNotConfiguredException>(() => _sut.GetRequiredAsync());
    }

    [Fact]
    public async Task IsDefaultPasswordAsync_MatchesOnlyTheConfiguredValue()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync(Stored("Secret@123"));

        Assert.True(await _sut.IsDefaultPasswordAsync("Secret@123"));
        Assert.False(await _sut.IsDefaultPasswordAsync("secret@123"));   // phân biệt hoa/thường
        Assert.False(await _sut.IsDefaultPasswordAsync("Other@12345"));
    }

    [Fact]
    public async Task IsDefaultPasswordAsync_NotConfigured_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync((SystemSetting?)null);

        Assert.False(await _sut.IsDefaultPasswordAsync("anything1"));
    }

    // ----- Nâng cấp dữ liệu cũ lúc khởi động -----

    [Fact]
    public async Task EnsureEncryptedAsync_LegacyPlaintext_EncryptsInPlaceAndSaves()
    {
        var legacy = new SystemSetting { Id = 4, Key = "DefaultPassword", Value = "Legacy@123" };
        _repoMock.Setup(r => r.GetByKeyAsync("DefaultPassword")).ReturnsAsync(legacy);

        await _sut.EnsureEncryptedAsync();

        Assert.True(DefaultPasswordCipher.IsProtected(legacy.Value));
        Assert.Equal("Legacy@123", _cipher.Unprotect(legacy.Value));
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task EnsureEncryptedAsync_AlreadyEncryptedOrMissing_DoesNothing()
    {
        _repoMock.SetupSequence(r => r.GetByKeyAsync("DefaultPassword"))
            .ReturnsAsync(Stored("Secret@123"))
            .ReturnsAsync((SystemSetting?)null);

        await _sut.EnsureEncryptedAsync();
        await _sut.EnsureEncryptedAsync();

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
