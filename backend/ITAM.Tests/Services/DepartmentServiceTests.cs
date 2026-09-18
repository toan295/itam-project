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
}
