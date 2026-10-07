using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ITAM.Tests.Services;

public class UserServiceTests
{
    private const int AdminRoleId = 1;
    private const int ManagerRoleId = 2;
    private const int TechnicianRoleId = 3;

    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRoleRepository> _roleRepoMock = new();
    private readonly Mock<IDepartmentRepository> _departmentRepoMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var resetTokenHelper = new PasswordResetTokenHelper(Options.Create(new JwtSettings
        {
            Secret = "unit-test-secret-key-not-used-in-production-1234567890",
            Issuer = "EAIMS.Tests",
            Audience = "EAIMS.Tests.Client",
            ExpiryMinutes = 60,
        }));

        _sut = new UserService(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _departmentRepoMock.Object,
            resetTokenHelper,
            _auditLogServiceMock.Object,
            NullLogger<UserService>.Instance);

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
    public async Task CreateAsync_ValidRequest_IssuesSetupTokenAndNeverExposesPassword()
    {
        User? saved = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => { u.Id = 10; saved = u; })
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(10)).ReturnsAsync(() => saved);

        var dto = new CreateUserRequestDto { FullName = "New User", Email = "new@eaims.local", RoleId = TechnicianRoleId, DepartmentId = 1 };
        var result = await _sut.CreateAsync(dto, currentUserId: 1);

        Assert.NotNull(result.DevOnlySetupToken);
        Assert.Equal("new@eaims.local", result.User.Email);
        // Mật khẩu ban đầu phải là chuỗi ngẫu nhiên không đoán được, không phải rỗng/mặc định.
        Assert.False(string.IsNullOrWhiteSpace(saved!.PasswordHash));
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
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 1);

        Assert.False(result.IsActive);
        _userRepoMock.Verify(r => r.CountActiveUsersInRoleAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
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
    public async Task ResetPasswordAsync_ValidTarget_ReturnsSetupToken()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));

        var result = await _sut.ResetPasswordAsync(9, currentUserId: 1);

        Assert.NotNull(result.DevOnlySetupToken);
        Assert.Equal(9, result.User.Id);
    }
}
