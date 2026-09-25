using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Moq;

namespace ITAM.Tests.Services;

public class DepartmentServiceTests
{
    private readonly Mock<IDepartmentRepository> _repoMock = new();
    private readonly DepartmentService _sut;

    public DepartmentServiceTests()
    {
        _sut = new DepartmentService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsAndDoesNotAdd()
    {
        _repoMock.Setup(r => r.GetByNameAsync("Phong IT"))
            .ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });

        var dto = new CreateDepartmentRequestDto { Name = "Phong IT" };

        await Assert.ThrowsAsync<DepartmentNameAlreadyExistsException>(() => _sut.CreateAsync(dto));
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NameDiffersOnlyByWhitespace_StillTreatedAsDuplicate()
    {
        // Name được Trim() trước khi so khớp, nên "  Phong IT  " phải bị coi là trùng với "Phong IT" đã có.
        _repoMock.Setup(r => r.GetByNameAsync("Phong IT"))
            .ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });

        var dto = new CreateDepartmentRequestDto { Name = "  Phong IT  " };

        await Assert.ThrowsAsync<DepartmentNameAlreadyExistsException>(() => _sut.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_NewName_AddsDepartment()
    {
        _repoMock.Setup(r => r.GetByNameAsync(It.IsAny<string>())).ReturnsAsync((Department?)null);

        var dto = new CreateDepartmentRequestDto { Name = "Phong Kinh doanh", Description = "Mo ta" };
        var result = await _sut.CreateAsync(dto);

        Assert.Equal("Phong Kinh doanh", result.Name);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsDepartmentNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Department?)null);

        await Assert.ThrowsAsync<DepartmentNotFoundException>(() => _sut.GetByIdAsync(999));
    }

    // ----- UpdateAsync (UC-04) -----

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsDepartmentNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Department?)null);

        await Assert.ThrowsAsync<DepartmentNotFoundException>(
            () => _sut.UpdateAsync(99, new UpdateDepartmentRequestDto { Name = "X" }));
    }

    [Fact]
    public async Task UpdateAsync_NameTakenByAnotherDepartment_ThrowsNameAlreadyExists()
    {
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });
        _repoMock.Setup(r => r.GetByNameAsync("Phong Ke toan"))
            .ReturnsAsync(new Department { Id = 2, Name = "Phong Ke toan" });

        await Assert.ThrowsAsync<DepartmentNameAlreadyExistsException>(
            () => _sut.UpdateAsync(1, new UpdateDepartmentRequestDto { Name = "Phong Ke toan" }));

        _repoMock.Verify(r => r.Update(It.IsAny<Department>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_KeepsSameName_OnlyChangesDescription()
    {
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });

        var result = await _sut.UpdateAsync(1, new UpdateDepartmentRequestDto { Name = "Phong IT", Description = "  Mo ta moi  " });

        Assert.Equal("Mo ta moi", result.Description);
        _repoMock.Verify(r => r.GetByNameAsync(It.IsAny<string>()), Times.Never);
        _repoMock.Verify(r => r.Update(It.IsAny<Department>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SaveThrowsDbUpdateException_MapsToNameAlreadyExists()
    {
        // Race hiếm: unique index Department.Name ở DB là chốt chặn cuối.
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });
        _repoMock.Setup(r => r.GetByNameAsync("Moi")).ReturnsAsync((Department?)null);
        _repoMock.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("dup"));

        await Assert.ThrowsAsync<DepartmentNameAlreadyExistsException>(
            () => _sut.UpdateAsync(1, new UpdateDepartmentRequestDto { Name = "Moi" }));
    }

    // ----- DeleteAsync (UC-04 E1) -----

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsDepartmentNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Department?)null);

        await Assert.ThrowsAsync<DepartmentNotFoundException>(() => _sut.DeleteAsync(99));
    }

    [Fact]
    public async Task DeleteAsync_DepartmentInUse_ThrowsInUseAndDoesNotRemove()
    {
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Department { Id = 1, Name = "Phong IT" });
        _repoMock.Setup(r => r.IsReferencedAsync(1)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DepartmentInUseException>(() => _sut.DeleteAsync(1));

        _repoMock.Verify(r => r.Remove(It.IsAny<Department>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_UnusedDepartment_RemovesIt()
    {
        var department = new Department { Id = 5, Name = "Phong Moi" };
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(department);
        _repoMock.Setup(r => r.IsReferencedAsync(5)).ReturnsAsync(false);

        await _sut.DeleteAsync(5);

        _repoMock.Verify(r => r.Remove(department), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ForeignKeyRaceOnSave_MapsToInUse()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Department { Id = 5, Name = "Phong Moi" });
        _repoMock.Setup(r => r.IsReferencedAsync(5)).ReturnsAsync(false);
        _repoMock.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException("fk"));

        await Assert.ThrowsAsync<DepartmentInUseException>(() => _sut.DeleteAsync(5));
    }
}
