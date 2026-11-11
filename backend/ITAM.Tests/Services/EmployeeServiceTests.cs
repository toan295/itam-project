using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class EmployeeServiceTests
{
    private readonly Mock<IEmployeeRepository> _repoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IAccountActivationService> _activationMock = new();
    private readonly EmployeeService _sut;

    public EmployeeServiceTests()
    {
        _sut = new EmployeeService(_repoMock.Object, _userRepoMock.Object, _activationMock.Object, NullLogger<EmployeeService>.Instance,
            new Mock<IRoleRepository>().Object, new Helpers.RecordingExclusiveSection());
        _repoMock.Setup(r => r.DepartmentExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.NameExistsInDepartmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);
    }

    private static UpsertEmployeeRequestDto Dto(string name = "Nguyễn Văn An", int dept = 1, bool active = true) =>
        new() { FullName = name, DepartmentId = dept, Position = "  Kế toán  ", Email = "an@eaims.local", Phone = "0912345678", IsActive = active };

    private static Employee Existing(int id = 5, int dept = 1, bool active = true) => new()
    {
        Id = id, EmployeeCode = $"NV{id:D4}", FullName = "Nguyễn Văn An", DepartmentId = dept, IsActive = active, Email = "an@eaims.local", Phone = "0912345678", Position = "Kế toán",
        Department = new Department { Id = dept, Name = "Phòng" },
    };

    // ----- chuẩn hoá tên -----

    [Theory]
    [InlineData("  nguyễn   văn an ", "Nguyễn Văn An")]
    [InlineData("NGUYỄN VĂN AN", "Nguyễn Văn An")]
    [InlineData("Nguyễn Văn An", "Nguyễn Văn An")]
    [InlineData("đặng  thị  em", "Đặng Thị Em")]
    [InlineData("Đặng  Thị   Em (Trưởng nhóm)", "Đặng Thị Em (Trưởng nhóm)")]
    public void PersonNameNormalizer_ProducesConsistentName(string input, string expected) =>
        Assert.Equal(expected, PersonNameNormalizer.Normalize(input));

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_NormalizesNameAndGeneratesCodeFromId()
    {
        Employee? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Employee>()))
            .Callback<Employee>(e => { e.Id = 12; added = e; })
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(() => added);

        var result = await _sut.CreateAsync(Dto("nguyễn văn an"), "Admin IT", null);

        Assert.Equal("NV0012", result.EmployeeCode);
        Assert.Equal("Nguyễn Văn An", result.FullName);
        Assert.Equal("Kế toán", result.Position);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Exactly(2)); // lần 2 để đặt mã theo Id.
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameInDepartment_ThrowsConflict()
    {
        _repoMock.Setup(r => r.NameExistsInDepartmentAsync("Nguyễn Văn An", 1, null)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.CreateAsync(Dto(), "Admin IT", null));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Employee>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DepartmentDoesNotExist_ThrowsArgumentException()
    {
        _repoMock.Setup(r => r.DepartmentExistsAsync(9)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(Dto(dept: 9), "Admin IT", null));
    }

    [Fact]
    public async Task CreateAsync_ManagerOtherDepartment_ThrowsDepartmentForbidden()
    {
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.CreateAsync(Dto(dept: 2), "Manager", 1));
    }

    // ----- UpdateAsync -----

    [Fact]
    public async Task UpdateAsync_Deactivate_WhenHoldingAsset_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing());
        _repoMock.Setup(r => r.HasOpenAllocationAsync(5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.UpdateAsync(5, Dto(active: false), "Admin IT", null));
    }

    [Fact]
    public async Task UpdateAsync_ChangeDepartment_WhenHoldingAsset_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing(dept: 1));
        _repoMock.Setup(r => r.HasOpenAllocationAsync(5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.UpdateAsync(5, Dto(dept: 2), "Admin IT", null));
    }

    [Fact]
    public async Task UpdateAsync_Deactivate_WhenNoOpenAllocation_Succeeds()
    {
        var employee = Existing();
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _repoMock.Setup(r => r.HasOpenAllocationAsync(5)).ReturnsAsync(false);

        await _sut.UpdateAsync(5, Dto(active: false), "Admin IT", null);

        Assert.False(employee.IsActive);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ManagerOtherDepartment_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing(dept: 2));

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => _sut.UpdateAsync(5, Dto(dept: 1), "Manager", 1));
    }

    // ----- DeleteAsync -----

    [Fact]
    public async Task DeleteAsync_WithAllocationHistory_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing());
        _repoMock.Setup(r => r.HasAnyAllocationAsync(5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.DeleteAsync(5, "Admin IT", null));

        _repoMock.Verify(r => r.Remove(It.IsAny<Employee>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_NoHistory_Removes()
    {
        var employee = Existing();
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _repoMock.Setup(r => r.HasAnyAllocationAsync(5)).ReturnsAsync(false);

        await _sut.DeleteAsync(5, "Admin IT", null);

        _repoMock.Verify(r => r.Remove(employee), Times.Once);
    }

    // ----- phạm vi xem -----

    [Fact]
    public async Task GetPagedAsync_Manager_IsForcedToOwnDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(It.IsAny<EmployeeListFilter>(), 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(new EmployeeListFilter { DepartmentId = 2 }, 1, 20, "Manager", 1);

        _repoMock.Verify(r => r.GetPagedAsync(It.Is<EmployeeListFilter>(f => f.DepartmentId == 1 && f.ExcludeStaffAccounts), 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Admin_CanFilterAnyDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(It.IsAny<EmployeeListFilter>(), 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(new EmployeeListFilter { DepartmentId = 2, Keyword = "an", IsActive = true }, 1, 20, "Admin IT", null);

        _repoMock.Verify(r => r.GetPagedAsync(
            It.Is<EmployeeListFilter>(f => f.DepartmentId == 2 && f.Keyword == "an" && f.IsActive == true && !f.ExcludeStaffAccounts), 1, 20), Times.Once);
    }

    // ----- Số điện thoại + tài khoản đăng nhập của nhân viên -----

    [Fact]
    public async Task CreateAsync_NormalizesPhoneNumber()
    {
        Employee? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Employee>())).Callback<Employee>(e => { e.Id = 12; added = e; }).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(() => added);
        var dto = Dto();
        dto.Phone = " 0912 345-678 ";

        await _sut.CreateAsync(dto, "Admin IT", null);

        Assert.Equal("0912345678", added!.Phone);
    }

    [Fact]
    public async Task UpdateAsync_PhoneChanged_SendsVerificationAgain()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing());
        var dto = Dto();
        dto.Phone = "0988888888";

        await _sut.UpdateAsync(5, dto, "Admin IT", null);
    }

    private static Employee WithAccount(int userId = 40)
    {
        var e = Existing();
        e.UserId = userId;
        return e;
    }

    [Fact]
    public async Task UpdateAsync_EmployeeWithAccount_SyncsNameEmailPhoneDepartmentAndActiveFlagToTheUser()
    {
        var employee = WithAccount(40);
        var user = new User { Id = 40, FullName = "Cũ", Email = "cu@eaims.local", DepartmentId = 9, IsActive = true };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetByEmailAsync("moi@eaims.local")).ReturnsAsync((User?)null);
        var dto = Dto(active: false);
        dto.Email = "moi@eaims.local";
        dto.Phone = "0977777777";

        await _sut.UpdateAsync(5, dto, "Admin IT", null);

        Assert.Equal("Nguyễn Văn An", user.FullName);
        Assert.Equal("moi@eaims.local", user.Email);
        Assert.Equal("0977777777", user.Phone);
        Assert.Equal(1, user.DepartmentId);
        Assert.False(user.IsActive);       // ngừng hoạt động => khoá tài khoản
    }

    [Fact]
    public async Task UpdateAsync_EmailTakenByAnotherUser_ConflictsAndDoesNotSave()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccount(40));
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(new User { Id = 40, Email = "cu@eaims.local" });
        _userRepoMock.Setup(r => r.GetByEmailAsync("moi@eaims.local")).ReturnsAsync(new User { Id = 99, Email = "moi@eaims.local" });
        var dto = Dto();
        dto.Email = "moi@eaims.local";

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.UpdateAsync(5, dto, "Admin IT", null));
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmailUsedByAnotherEmployee_ConflictsAndDoesNotSave()
    {
        _repoMock.Setup(r => r.EmailExistsAsync("an@eaims.local", null)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.CreateAsync(Dto(), "Admin IT", null));
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Employee>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmailUsedByExistingAccount_Conflicts()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("an@eaims.local")).ReturnsAsync(new User { Id = 7, Email = "an@eaims.local" });

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.CreateAsync(Dto(), "Admin IT", null));
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Employee>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingOwnLinkedProfile_IsRejected()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccount(40));

        await Assert.ThrowsAsync<EmployeeSelfDeactivationException>(
            () => _sut.UpdateAsync(5, Dto(active: false), "Admin IT", null, currentUserId: 40));
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_EmailUsedByAnotherEmployee_ConflictsAndDoesNotSave()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Existing());
        _repoMock.Setup(r => r.EmailExistsAsync("an@eaims.local", 5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.UpdateAsync(5, Dto(), "Admin IT", null));
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_EmailOfOwnLinkedAccount_IsNotAConflict()
    {
        var user = new User { Id = 40, Email = "an@eaims.local", IsActive = true };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccount(40));
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetByEmailAsync("an@eaims.local")).ReturnsAsync(user);

        await _sut.UpdateAsync(5, Dto(), "Admin IT", null);

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_EmployeeWithAccount_Conflicts()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccount());

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.DeleteAsync(5, "Admin IT", null));
        _repoMock.Verify(r => r.Remove(It.IsAny<Employee>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_EmailChangedWhileAccountPendingActivation_InvalidatesTheOldLink()
    {
        var employee = WithAccount(40);
        var user = new User { Id = 40, FullName = "Cũ", Email = "cu@gmail.com", DepartmentId = 1, IsActive = true, PendingActivation = true };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetByEmailAsync("moi@gmail.com")).ReturnsAsync((User?)null);
        var dto = Dto();
        dto.Email = "moi@gmail.com";

        await _sut.UpdateAsync(5, dto, "Admin IT", null);

        _activationMock.Verify(a => a.InvalidatePendingAsync(40), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_EmailUnchangedWhileAccountPending_KeepsTheLink()
    {
        var employee = WithAccount(40);
        var user = new User { Id = 40, FullName = "Cũ", Email = "an@eaims.local", DepartmentId = 1, IsActive = true, PendingActivation = true };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(user);

        await _sut.UpdateAsync(5, Dto(), "Admin IT", null);

        _activationMock.Verify(a => a.InvalidatePendingAsync(It.IsAny<int>()), Times.Never);
    }

    private static Employee WithAccountRole(string roleName, int userId = 40, int dept = 1)
    {
        var e = Existing(dept: dept);
        e.UserId = userId;
        e.User = new User { Id = userId, Email = e.Email!, FullName = e.FullName, DepartmentId = dept, IsActive = true, RoleId = 9, Role = new Role { Id = 9, Name = roleName } };
        return e;
    }

    [Theory]
    [InlineData("Admin IT")]
    [InlineData("Manager")]
    [InlineData("Technician")]
    public async Task UpdateAsync_ManagerEditingProfileLinkedToAStaffAccount_IsHiddenAsNotFound(string roleName)
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccountRole(roleName));

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => _sut.UpdateAsync(5, Dto(), "Manager", 1));
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ManagerEditingProfileLinkedToAnEmployeeAccount_IsAllowed()
    {
        var employee = WithAccountRole("Employee");
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(employee.User);

        await _sut.UpdateAsync(5, Dto(), "Manager", 1);

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteAsync_ManagerDeletingProfileLinkedToAStaffAccount_IsHiddenAsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccountRole("Technician"));

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => _sut.DeleteAsync(5, "Manager", 1));
    }

    [Fact]
    public async Task UpdateAsync_AdminEditingStaffProfile_SyncsToTheAccount()
    {
        var employee = WithAccountRole("Manager");
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(employee.User);
        var dto = Dto();
        dto.Phone = "0966666666";

        await _sut.UpdateAsync(5, dto, "Admin IT", null);

        Assert.Equal("0966666666", employee.User!.Phone);
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingTheLastActiveAdminsProfile_Conflicts()
    {
        var employee = WithAccountRole("Admin IT");
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(employee.User);
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(9, 40)).ReturnsAsync(0);

        await Assert.ThrowsAsync<EmployeeConflictException>(() => _sut.UpdateAsync(5, Dto(active: false), "Admin IT", null));
        Assert.True(employee.User!.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingAnAdminsProfileWhenAnotherAdminExists_LocksTheAccount()
    {
        var employee = WithAccountRole("Admin IT");
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(employee);
        _userRepoMock.Setup(r => r.GetByIdTrackedAsync(40)).ReturnsAsync(employee.User);
        _userRepoMock.Setup(r => r.CountActiveUsersInRoleAsync(9, 40)).ReturnsAsync(1);

        await _sut.UpdateAsync(5, Dto(active: false), "Admin IT", null);

        Assert.False(employee.User!.IsActive);
    }

    [Theory]
    [InlineData("Admin IT")]
    [InlineData("Manager")]
    [InlineData("Technician")]
    public async Task GetByIdAsync_ManagerOpeningAStaffProfileOfTheirOwnDepartment_IsNotFound(string roleName)
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccountRole(roleName));

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => _sut.GetByIdAsync(5, "Manager", 1));
    }

    [Fact]
    public async Task GetByIdAsync_ManagerOpeningAnOrdinaryEmployeeOfTheirDepartment_Works()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(WithAccountRole("Employee"));

        var result = await _sut.GetByIdAsync(5, "Manager", 1);

        Assert.Equal("Employee", result.RoleName);
    }

    [Fact]
    public async Task GetPagedAsync_Admin_SeesStaffProfilesToo()
    {
        _repoMock.Setup(r => r.GetPagedAsync(It.IsAny<EmployeeListFilter>(), 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(new EmployeeListFilter(), 1, 20, "Admin IT", null);

        _repoMock.Verify(r => r.GetPagedAsync(It.Is<EmployeeListFilter>(f => !f.ExcludeStaffAccounts && f.DepartmentId == null), 1, 20), Times.Once);
    }

    [Theory]
    [InlineData("accountState", "banned")]
    [InlineData("roleName", "Root")]
    [InlineData("sort", "random")]
    public async Task GetPagedAsync_InvalidAdvancedFilter_ThrowsArgumentException(string field, string value)
    {
        var filter = new EmployeeListFilter();
        switch (field)
        {
            case "accountState": filter.AccountState = value; break;
            case "roleName": filter.RoleName = value; break;
            default: filter.Sort = value; break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPagedAsync(filter, 1, 20, "Admin IT", null));
        _repoMock.Verify(r => r.GetPagedAsync(It.IsAny<EmployeeListFilter>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_ClientCannotTurnOffStaffExclusionForManager()
    {
        _repoMock.Setup(r => r.GetPagedAsync(It.IsAny<EmployeeListFilter>(), 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(new EmployeeListFilter { ExcludeStaffAccounts = false, RoleName = "Admin IT" }, 1, 20, "Manager", 3);

        _repoMock.Verify(r => r.GetPagedAsync(It.Is<EmployeeListFilter>(f => f.ExcludeStaffAccounts && f.DepartmentId == 3), 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPositionSuggestionsAsync_Manager_IsScopedToOwnDepartmentAndOrdinaryEmployees()
    {
        _repoMock.Setup(r => r.GetPositionSuggestionsAsync(4, true, 50)).ReturnsAsync(new List<string> { "Kế toán" });

        var result = await _sut.GetPositionSuggestionsAsync("Manager", 4);

        Assert.Equal(new[] { "Kế toán" }, result);
    }
}
