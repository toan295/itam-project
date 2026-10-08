using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class UserServiceTests
{
    private const int AdminRoleId = 1;
    private const int ManagerRoleId = 2;
    private const int TechnicianRoleId = 3;
    private const string DefaultPassword = "Default@123";

    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRoleRepository> _roleRepoMock = new();
    private readonly Mock<IDepartmentRepository> _departmentRepoMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IDefaultPasswordService> _defaultPasswordMock = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _departmentRepoMock.Object,
            _defaultPasswordMock.Object,
            _auditLogServiceMock.Object,
            NullLogger<UserService>.Instance);

        _defaultPasswordMock.Setup(s => s.GetRequiredAsync()).ReturnsAsync(DefaultPassword);
        _roleRepoMock.Setup(r => r.ExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _departmentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _auditLogServiceMock
            .Setup(x => x.RecordAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<object?>(),
                It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
    }

    private static User CreateUser(int id, int roleId, string roleName, bool isActive = true, string email = "user@eaims.local") => new()
    {
        Id = id,
        FullName = "Test User",
        Email = email,
        PasswordHash = "hash",
        RoleId = roleId,
        Role = new Role { Id = roleId, Name = roleName },
        DepartmentId = 1,
        Department = new Department { Id = 1, Name = "Phong IT" },
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
    };

    private static UpdateUserRequestDto ValidUpdateDto(int roleId = AdminRoleId, bool isActive = true) => new()
    {
        FullName = "Updated Name",
        Email = "user@eaims.local",
        RoleId = roleId,
        DepartmentId = 1,
        IsActive = isActive,
    };

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ThrowsAndDoesNotAdd()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("existing@eaims.local"))
            .ReturnsAsync(CreateUser(1, TechnicianRoleId, "Technician"));

        var dto = new CreateUserRequestDto { FullName = "New User", Email = "existing@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 1 };

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => _sut.CreateAsync(dto, currentUserId: 1));
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RoleDoesNotExist_ThrowsArgumentException()
    {
        _roleRepoMock.Setup(r => r.ExistsAsync(It.IsAny<int>())).ReturnsAsync(false);
        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = 999, DepartmentId = 1 };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(dto, currentUserId: 1));
    }

    [Fact]
    public async Task CreateAsync_DepartmentDoesNotExist_ThrowsArgumentException()
    {
        _departmentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);
        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 999 };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(dto, currentUserId: 1));
    }

    [Fact]
    public async Task CreateAsync_DefaultPasswordNotConfigured_ThrowsAndDoesNotAdd()
    {
        _defaultPasswordMock.Setup(s => s.GetRequiredAsync()).ThrowsAsync(new DefaultPasswordNotConfiguredException());

        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 1 };
        await Assert.ThrowsAsync<DefaultPasswordNotConfiguredException>(() => _sut.CreateAsync(dto, currentUserId: 1));

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_UsesDefaultPassword()
    {
        User? saved = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => { u.Id = 10; saved = u; })
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(10)).ReturnsAsync(() => saved);

        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 1 };
        var result = await _sut.CreateAsync(dto, currentUserId: 1);

        Assert.Equal("new@eaims.local", result.Email);
        Assert.NotEqual(DefaultPassword, saved!.PasswordHash); // chỉ lưu hash, không lưu plaintext.
        Assert.True(BCrypt.Net.BCrypt.Verify(DefaultPassword, saved.PasswordHash));
        Assert.True(saved.MustChangePassword); // buộc đổi mật khẩu ở lần đăng nhập đầu.
    }

    // ----- UpdateAsync -----

    [Fact]
    public async Task UpdateAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _sut.UpdateAsync(999, ValidUpdateDto(), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_SelfLock_ThrowsSelfLockNotAllowedException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(CreateUser(5, TechnicianRoleId, "Technician"));

        await Assert.ThrowsAsync<SelfLockNotAllowedException>(
            () => _sut.UpdateAsync(5, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 5));
    }

    [Fact]
    public async Task UpdateAsync_LockingLastActiveAdmin_ThrowsLastAdminProtectionException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(0);

        await Assert.ThrowsAsync<LastAdminProtectionException>(
            () => _sut.UpdateAsync(7, ValidUpdateDto(roleId: AdminRoleId, isActive: false), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_DemotingLastActiveAdminToManager_ThrowsLastAdminProtectionException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(0);

        // isActive vẫn true nhưng đổi RoleId sang Manager -> vẫn phải bị chặn vì mất quyền Admin IT.
        await Assert.ThrowsAsync<LastAdminProtectionException>(
            () => _sut.UpdateAsync(7, ValidUpdateDto(roleId: ManagerRoleId, isActive: true), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_LockingAdminWhenAnotherActiveAdminExists_Succeeds()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(1);

        var result = await _sut.UpdateAsync(7, ValidUpdateDto(roleId: AdminRoleId, isActive: false), currentUserId: 1);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_LockingNonAdminUser_DoesNotCheckLastAdminProtection()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, ManagerRoleId, "Manager"));

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: ManagerRoleId, isActive: false), currentUserId: 1);

        Assert.False(result.IsActive);
        _userRepoMock.Verify(r => r.CountActiveUsersInRoleAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_LockingTechnicianBelowMinimum_SucceedsWithWarning()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(TechnicianRoleId, 9)).ReturnsAsync(2);

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 1);

        Assert.False(result.IsActive); // vẫn khoá được.
        Assert.Contains("2 kỹ thuật viên", result.Warning);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DemotingTechnicianBelowMinimum_SucceedsWithWarning()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(TechnicianRoleId, 9)).ReturnsAsync(2);

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: ManagerRoleId), currentUserId: 1);

        Assert.Equal(ManagerRoleId, result.RoleId);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task UpdateAsync_LockingTechnicianWithEnoughOthers_Succeeds()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(TechnicianRoleId, 9)).ReturnsAsync(3);

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 1);

        Assert.False(result.IsActive);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task UpdateAsync_EmailChangedToExisting_ThrowsEmailAlreadyExistsException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(3)).ReturnsAsync(CreateUser(3, TechnicianRoleId, "Technician", email: "old@eaims.local"));
        _userRepoMock.Setup(r => r.GetByEmailAsync("taken@eaims.local")).ReturnsAsync(CreateUser(4, TechnicianRoleId, "Technician", email: "taken@eaims.local"));

        var dto = ValidUpdateDto(roleId: TechnicianRoleId);
        dto.Email = "taken@eaims.local";

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => _sut.UpdateAsync(3, dto, currentUserId: 1));
    }

    // ----- ResetPasswordAsync -----

    [Fact]
    public async Task ResetPasswordAsync_TargetingSelf_ThrowsSelfPasswordResetNotAllowedException()
    {
        await Assert.ThrowsAsync<SelfPasswordResetNotAllowedException>(
            () => _sut.ResetPasswordAsync(5, currentUserId: 5));

        _userRepoMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() => _sut.ResetPasswordAsync(999, currentUserId: 1));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidTarget_SetsPasswordToDefault()
    {
        var target = CreateUser(9, TechnicianRoleId, "Technician");
        target.PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@1", 4);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(target);

        var result = await _sut.ResetPasswordAsync(9, currentUserId: 1);

        Assert.Equal(9, result.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify(DefaultPassword, target.PasswordHash));
        Assert.True(target.MustChangePassword);
    }

    // ----- Audit Log (Unit-of-Work: RecordAsync không tự lưu, UserService quyết định số lần SaveChanges) -----

    [Fact]
    public async Task CreateAsync_ValidRequest_RecordsCreateWithNewIdAndSavesTwice()
    {
        User? saved = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => { u.Id = 10; saved = u; })
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(10)).ReturnsAsync(() => saved);

        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 1 };
        await _sut.CreateAsync(dto, currentUserId: 1);

        _auditLogServiceMock.Verify(a => a.RecordAsync(1, "Create", "User", 10, null, It.IsNotNull<object>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_RecordsBeforeAndAfterSnapshotAndSavesOnce()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(TechnicianRoleId, 9)).ReturnsAsync(5);
        object? oldValue = null, newValue = null;
        _auditLogServiceMock
            .Setup(a => a.RecordAsync(1, "Update", "User", 9, It.IsAny<object?>(), It.IsAny<object?>()))
            .Callback<int, string, string, int, object?, object?>((_, _, _, _, o, n) => { oldValue = o; newValue = n; })
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync(9, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 1);

        Assert.NotNull(oldValue);
        Assert.NotNull(newValue);
        Assert.Contains("IsActive = True", oldValue!.ToString());
        Assert.Contains("IsActive = False", newValue!.ToString());
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_BusinessRuleViolated_DoesNotRecordAudit()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(CreateUser(1, AdminRoleId, "Admin IT"));

        await Assert.ThrowsAsync<SelfLockNotAllowedException>(() =>
            _sut.UpdateAsync(1, ValidUpdateDto(roleId: AdminRoleId, isActive: false), currentUserId: 1));

        _auditLogServiceMock.Verify(a => a.RecordAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidTarget_RecordsResetPasswordAndSavesOnce()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));

        await _sut.ResetPasswordAsync(9, currentUserId: 1);

        _auditLogServiceMock.Verify(a => a.RecordAsync(1, "ResetPassword", "User", 9, null, It.IsAny<object?>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
