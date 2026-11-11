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
    private const int EmployeeRoleId = 4;

    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRoleRepository> _roleRepoMock = new();
    private readonly Mock<IDepartmentRepository> _departmentRepoMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IAccountActivationService> _activationMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Helpers.RecordingExclusiveSection _exclusive = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(
            _userRepoMock.Object,
            _roleRepoMock.Object,
            _departmentRepoMock.Object,
            _auditLogServiceMock.Object,
            _activationMock.Object,
            _employeeRepoMock.Object,
            NullLogger<UserService>.Instance,
            _exclusive);

        _activationMock.Setup(a => a.GetStatusesAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync((IEnumerable<int> ids) => ids.ToDictionary(id => id, _ => new ActivationStatusDto()));
        _activationMock.Setup(a => a.SendAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new EmailDispatchDto { Sent = true, Mode = "smtp", Message = "Đã gửi" });
        _roleRepoMock.Setup(r => r.ExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _roleRepoMock.Setup(r => r.HasNameAsync(EmployeeRoleId, "Employee")).ReturnsAsync(true);
        _departmentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _auditLogServiceMock
            .Setup(x => x.RecordAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()))
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

    private static UpdateUserRequestDto ValidUpdateDto(int roleId = AdminRoleId, bool isActive = true) => new() { RoleId = roleId, IsActive = isActive };

    private static Employee MakeEmployee(int id = 7, bool active = true, string? email = "nv0007@gmail.com", int? userId = null) => new()
    {
        Id = id, EmployeeCode = $"NV{id:D4}", FullName = "Lê Thị Hoa", DepartmentId = 1, IsActive = active, Email = email,
        Phone = "0912345007", UserId = userId, Department = new Department { Id = 1, Name = "Phong IT" },
    };

    private User? _saved;

    private void SetupCreate(Employee? employee = null)
    {
        var e = employee ?? MakeEmployee();
        _employeeRepoMock.Setup(r => r.GetByIdAsync(e.Id)).ReturnsAsync(e);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>())).Callback<User>(u => { u.Id = 10; _saved = u; }).Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(10)).ReturnsAsync(() =>
        {
            _saved!.Role = new Role { Id = _saved.RoleId, Name = _saved.RoleId == TechnicianRoleId ? "Technician" : "Manager" };
            _saved.Department = new Department { Id = 1, Name = "Phong IT" };
            return _saved;
        });
    }

    // ----- CreateAsync: mọi tài khoản đều từ một nhân viên có sẵn -----

    [Fact]
    public async Task CreateAsync_WithoutEmployee_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = TechnicianRoleId, EmployeeId = 0 }, 1));
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmployeeNotFound_IsRejected()
    {
        _employeeRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Employee?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = TechnicianRoleId, EmployeeId = 99 }, 1));
    }

    [Fact]
    public async Task CreateAsync_RoleDoesNotExist_IsRejected()
    {
        _roleRepoMock.Setup(r => r.ExistsAsync(999)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = 999, EmployeeId = 7 }, 1));
    }

    [Fact]
    public async Task CreateAsync_EmployeeAlreadyHasAccount_Conflicts()
    {
        _employeeRepoMock.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(MakeEmployee(userId: 30));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = ManagerRoleId, EmployeeId = 7 }, 1));
    }

    [Fact]
    public async Task CreateAsync_InactiveEmployee_IsRejected()
    {
        _employeeRepoMock.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(MakeEmployee(active: false));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = ManagerRoleId, EmployeeId = 7 }, 1));
    }

    [Fact]
    public async Task CreateAsync_EmployeeWithoutEmail_AsksForEmailFirst()
    {
        _employeeRepoMock.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(MakeEmployee(email: null));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = ManagerRoleId, EmployeeId = 7 }, 1));
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmailUsedByAnotherAccount_Conflicts()
    {
        _employeeRepoMock.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(MakeEmployee());
        _userRepoMock.Setup(r => r.GetByEmailAsync("nv0007@gmail.com")).ReturnsAsync(CreateUser(2, ManagerRoleId, "Manager", email: "nv0007@gmail.com"));

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => _sut.CreateAsync(new CreateUserRequestDto { RoleId = ManagerRoleId, EmployeeId = 7 }, 1));
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        _activationMock.Verify(a => a.SendAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(AdminRoleId)]
    [InlineData(ManagerRoleId)]
    [InlineData(TechnicianRoleId)]
    [InlineData(EmployeeRoleId)]
    public async Task CreateAsync_AnyRole_BuildsPendingAccountFromTheEmployee_LinksIt_AndSendsActivationEmail(int roleId)
    {
        var employee = MakeEmployee();
        SetupCreate(employee);

        var result = await _sut.CreateAsync(new CreateUserRequestDto { RoleId = roleId, EmployeeId = 7 }, 1);

        // Thông tin lấy từ hồ sơ nhân viên.
        Assert.Equal("Lê Thị Hoa", _saved!.FullName);
        Assert.Equal("nv0007@gmail.com", _saved.Email);
        Assert.Equal("0912345007", _saved.Phone);
        Assert.Equal(1, _saved.DepartmentId);
        Assert.Equal(roleId, _saved.RoleId);
        // Chờ kích hoạt, không có mật khẩu ai biết.
        Assert.True(_saved.PendingActivation);
        Assert.False(_saved.MustChangePassword);
        Assert.False(string.IsNullOrEmpty(_saved.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("Default@123", _saved.PasswordHash));
        Assert.Same(_saved, employee.User);                 // liên kết 1-1 cùng một lần lưu
        _activationMock.Verify(a => a.SendAsync(10, IAccountActivationService.ReasonCreated), Times.Once);
        Assert.True(result.PendingActivation);
        Assert.True(result.ActivationDispatch!.Sent);
    }

    [Fact]
    public async Task CreateAsync_ActivationEmailFails_AccountIsStillCreated()
    {
        SetupCreate();
        _activationMock.Setup(a => a.SendAsync(10, It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("smtp"));

        var result = await _sut.CreateAsync(new CreateUserRequestDto { RoleId = TechnicianRoleId, EmployeeId = 7 }, 1);

        Assert.True(result.PendingActivation);
        Assert.False(result.ActivationDispatch!.Sent);
    }

    [Fact]
    public async Task CreateAsync_RecordsCreateWithNewIdAndSavesTwice()
    {
        SetupCreate();

        await _sut.CreateAsync(new CreateUserRequestDto { RoleId = TechnicianRoleId, EmployeeId = 7 }, currentUserId: 1);

        _auditLogServiceMock.Verify(a => a.RecordAsync(1, "Create", "User", 10, null, It.IsNotNull<object>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GetEmployeeCandidates_MapsEmployeesWithoutAccount()
    {
        _employeeRepoMock.Setup(r => r.GetAccountCandidatesAsync("hoa", 50)).ReturnsAsync(new List<Employee> { MakeEmployee() });

        var list = await _sut.GetEmployeeCandidatesAsync("hoa");

        var c = Assert.Single(list);
        Assert.Equal("NV0007", c.EmployeeCode);
        Assert.Equal("Phong IT", c.DepartmentName);
    }

    // ----- UpdateAsync: chỉ đổi vai trò + khoá -----

    [Fact]
    public async Task UpdateAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() => _sut.UpdateAsync(999, ValidUpdateDto(), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_SelfLock_ThrowsSelfLockNotAllowedException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(CreateUser(5, TechnicianRoleId, "Technician"));

        await Assert.ThrowsAsync<SelfLockNotAllowedException>(
            () => _sut.UpdateAsync(5, ValidUpdateDto(roleId: TechnicianRoleId, isActive: false), currentUserId: 5));
    }

    [Fact]
    public async Task UpdateAsync_SelfRoleChange_ThrowsSelfRoleChangeNotAllowedException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(5)).ReturnsAsync(CreateUser(5, AdminRoleId, "Admin IT"));

        await Assert.ThrowsAsync<SelfRoleChangeNotAllowedException>(
            () => _sut.UpdateAsync(5, ValidUpdateDto(roleId: ManagerRoleId), currentUserId: 5));
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
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

        await Assert.ThrowsAsync<LastAdminProtectionException>(
            () => _sut.UpdateAsync(7, ValidUpdateDto(roleId: ManagerRoleId, isActive: true), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_AdminAccount_RunsInsideTheAdminRoleLock_SoTwoAdminsCannotDemoteEachOtherAtOnce()
    {
        _roleRepoMock.Setup(r => r.GetIdByNameAsync("Admin IT")).ReturnsAsync(AdminRoleId);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(1);

        await _sut.UpdateAsync(7, ValidUpdateDto(roleId: AdminRoleId, isActive: false), currentUserId: 1);

        Assert.Contains((ITAM.API.Repositories.Interfaces.LockTarget.Role, AdminRoleId), _exclusive.Calls);
    }

    [Fact]
    public async Task DeleteAsync_AdminAccount_RunsInsideTheAdminRoleLock()
    {
        _roleRepoMock.Setup(r => r.GetIdByNameAsync("Admin IT")).ReturnsAsync(AdminRoleId);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(1);

        await _sut.DeleteAsync(7, currentUserId: 1);

        Assert.Contains((ITAM.API.Repositories.Interfaces.LockTarget.Role, AdminRoleId), _exclusive.Calls);
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

        Assert.False(result.IsActive);
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
    public async Task UpdateAsync_ChangingRoleBetweenStaffRoles_Works_AndLeavesPersonalInfoAlone()
    {
        var user = CreateUser(9, ManagerRoleId, "Manager");
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(user);

        var result = await _sut.UpdateAsync(9, ValidUpdateDto(roleId: TechnicianRoleId), currentUserId: 1);

        Assert.Equal(TechnicianRoleId, user.RoleId);
        Assert.Equal("Test User", user.FullName);        // họ tên/email/SĐT/phòng ban chỉ đổi ở Danh mục nhân viên
        Assert.Equal("user@eaims.local", user.Email);
        Assert.Equal(TechnicianRoleId, result.RoleId);
    }

    [Fact]
    public async Task UpdateAsync_MovingAStaffAccountIntoTheEmployeeRole_IsRejected()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, ManagerRoleId, "Manager"));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateAsync(9, ValidUpdateDto(roleId: EmployeeRoleId), currentUserId: 1));
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_MovingAnEmployeeAccountOutOfTheEmployeeRole_IsRejected()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(CreateUser(40, EmployeeRoleId, "Employee"));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateAsync(40, ValidUpdateDto(roleId: ManagerRoleId), currentUserId: 1));
    }

    [Fact]
    public async Task UpdateAsync_EmployeeAccount_CanBeLockedWithoutChangingRole()
    {
        var user = CreateUser(40, EmployeeRoleId, "Employee");
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(user);

        var result = await _sut.UpdateAsync(40, ValidUpdateDto(roleId: EmployeeRoleId, isActive: false), currentUserId: 1);

        Assert.True(result.IsEmployeeAccount);
        Assert.False(user.IsActive);
    }

    // ----- ResetPasswordAsync -----

    [Fact]
    public async Task ResetPasswordAsync_TargetingSelf_ThrowsSelfPasswordResetNotAllowedException()
    {
        await Assert.ThrowsAsync<SelfPasswordResetNotAllowedException>(() => _sut.ResetPasswordAsync(5, currentUserId: 5));

        _userRepoMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() => _sut.ResetPasswordAsync(999, currentUserId: 1));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidTarget_InvalidatesOldPassword_AndSendsResetLink()
    {
        var target = CreateUser(9, TechnicianRoleId, "Technician");
        var oldHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@1", 4);
        target.PasswordHash = oldHash;
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(target);

        var result = await _sut.ResetPasswordAsync(9, currentUserId: 1);

        Assert.Equal(9, result.Id);
        Assert.NotEqual(oldHash, target.PasswordHash);
        Assert.False(BCrypt.Net.BCrypt.Verify("OldPassword@1", target.PasswordHash));   // mật khẩu cũ mất hiệu lực ngay
        Assert.True(target.PendingActivation);
        Assert.False(target.MustChangePassword);
        _activationMock.Verify(a => a.SendAsync(9, IAccountActivationService.ReasonReset), Times.Once);
        Assert.True(result.ActivationDispatch!.Sent);
    }

    [Fact]
    public async Task ResetPasswordAsync_AccountPendingActivation_IsRejected()
    {
        var target = CreateUser(9, TechnicianRoleId, "Technician");
        target.PendingActivation = true;
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(target);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ResetPasswordAsync(9, currentUserId: 1));
        _activationMock.Verify(a => a.SendAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_LockedAccount_IsRejected()
    {
        var target = CreateUser(9, TechnicianRoleId, "Technician");
        target.IsActive = false;
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(target);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ResetPasswordAsync(9, currentUserId: 1));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidTarget_RecordsResetPasswordAndSavesOnce()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));

        await _sut.ResetPasswordAsync(9, currentUserId: 1);

        _auditLogServiceMock.Verify(a => a.RecordAsync(1, "ResetPassword", "User", 9, null, It.IsAny<object?>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ----- Gửi lại email kích hoạt -----

    private static User PendingAccount(int roleId = TechnicianRoleId, string roleName = "Technician")
    {
        var user = CreateUser(40, roleId, roleName);
        user.PendingActivation = true;
        return user;
    }

    [Fact]
    public async Task ResendActivation_PendingAccount_SendsResentEmailForAnyRole()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(PendingAccount());

        var result = await _sut.ResendActivationAsync(40, 1);

        _activationMock.Verify(a => a.SendAsync(40, IAccountActivationService.ReasonResent), Times.Once);
        Assert.True(result.ActivationDispatch!.Sent);
    }

    [Fact]
    public async Task ResendActivation_AlreadyActivated_IsRejected()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(CreateUser(40, TechnicianRoleId, "Technician"));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ResendActivationAsync(40, 1));
        _activationMock.Verify(a => a.SendAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResendActivation_LockedAccount_IsRejected()
    {
        var user = PendingAccount();
        user.IsActive = false;
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(40)).ReturnsAsync(user);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ResendActivationAsync(40, 1));
    }

    // ----- DeleteAsync: xoá tài khoản (xoá mềm) -----

    [Fact]
    public async Task DeleteAsync_Self_IsRejected()
    {
        await Assert.ThrowsAsync<SelfDeleteNotAllowedException>(() => _sut.DeleteAsync(5, currentUserId: 5));
        _userRepoMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_UserNotFound_Throws()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() => _sut.DeleteAsync(999, currentUserId: 1));
    }

    [Fact]
    public async Task DeleteAsync_LastActiveAdmin_IsProtected()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(CreateUser(7, AdminRoleId, "Admin IT"));
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(AdminRoleId, 7)).ReturnsAsync(0);

        await Assert.ThrowsAsync<LastAdminProtectionException>(() => _sut.DeleteAsync(7, currentUserId: 1));
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_LockedAdmin_DoesNotNeedAnotherAdmin()
    {
        // Admin đã bị khoá không còn được tính là admin đang hoạt động -> xoá không làm mất người quản trị nào.
        var admin = CreateUser(7, AdminRoleId, "Admin IT", isActive: false);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(admin);

        await _sut.DeleteAsync(7, currentUserId: 1);

        Assert.True(admin.IsDeleted);
        _userRepoMock.Verify(r => r.CountActiveUsersInRoleAsync(It.IsAny<int>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_TechnicianWithPendingTickets_IsBlocked()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _userRepoMock.Setup(r => r.CountPendingAssignedTicketsAsync(9)).ReturnsAsync(2);

        var ex = await Assert.ThrowsAsync<UserHasPendingWorkException>(() => _sut.DeleteAsync(9, currentUserId: 1));

        Assert.Contains("2 phiếu", ex.Message);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Valid_TombstonesTheAccount_UnlinksEmployee_InvalidatesLinks_AndAuditsOnce()
    {
        var user = CreateUser(9, ManagerRoleId, "Manager", email: "quanly@gmail.com");
        user.PendingActivation = true;
        var oldHash = user.PasswordHash;
        var employee = MakeEmployee(userId: 9);
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(user);
        _employeeRepoMock.Setup(r => r.GetByUserIdTrackedAsync(9)).ReturnsAsync(employee);

        await _sut.DeleteAsync(9, currentUserId: 1);

        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.False(user.IsActive);
        Assert.False(user.PendingActivation);
        Assert.NotEqual("quanly@gmail.com", user.Email);                // email được giải phóng
        Assert.EndsWith("@deleted.invalid", user.Email);
        Assert.NotEqual(oldHash, user.PasswordHash);                    // mật khẩu bị huỷ
        Assert.Null(employee.UserId);                                    // hồ sơ nhân viên còn nguyên, chỉ tách liên kết
        _activationMock.Verify(a => a.InvalidatePendingAsync(9), Times.Once);
        _userRepoMock.Verify(r => r.Update(user), Times.Once);
        _auditLogServiceMock.Verify(a => a.RecordAsync(1, "Delete", "User", 9, It.IsNotNull<object>(), It.IsNotNull<object>()), Times.Once);
        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);    // một lần lưu: tài khoản + hồ sơ + nhật ký cùng commit
    }

    [Fact]
    public async Task DeleteAsync_AccountWithoutEmployeeProfile_StillWorks()
    {
        _userRepoMock.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(CreateUser(9, TechnicianRoleId, "Technician"));
        _employeeRepoMock.Setup(r => r.GetByUserIdTrackedAsync(9)).ReturnsAsync((Employee?)null);

        await _sut.DeleteAsync(9, currentUserId: 1);

        _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ----- Audit Log (Unit-of-Work: RecordAsync không tự lưu, UserService quyết định số lần SaveChanges) -----

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
}
