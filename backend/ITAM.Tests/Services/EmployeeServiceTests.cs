using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Moq;

namespace ITAM.Tests.Services;

public class EmployeeServiceTests
{
    private readonly Mock<IEmployeeRepository> _repoMock = new();
    private readonly EmployeeService _sut;

    public EmployeeServiceTests()
    {
        _sut = new EmployeeService(_repoMock.Object);
        _repoMock.Setup(r => r.DepartmentExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.NameExistsInDepartmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);
    }

    private static UpsertEmployeeRequestDto Dto(string name = "Nguyễn Văn An", int dept = 1, bool active = true) =>
        new() { FullName = name, DepartmentId = dept, Position = "  Kế toán  ", IsActive = active };

    private static Employee Existing(int id = 5, int dept = 1, bool active = true) => new()
    {
        Id = id, EmployeeCode = $"NV{id:D4}", FullName = "Nguyễn Văn An", DepartmentId = dept, IsActive = active,
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
        _repoMock.Setup(r => r.GetPagedAsync(1, null, null, 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(2, null, null, 1, 20, "Manager", 1);

        _repoMock.Verify(r => r.GetPagedAsync(1, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Admin_CanFilterAnyDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(2, "an", true, 1, 20)).ReturnsAsync((new List<Employee>(), 0));

        await _sut.GetPagedAsync(2, "an", true, 1, 20, "Admin IT", null);

        _repoMock.Verify(r => r.GetPagedAsync(2, "an", true, 1, 20), Times.Once);
    }
}
