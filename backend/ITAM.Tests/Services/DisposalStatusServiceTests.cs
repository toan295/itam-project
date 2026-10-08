using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Moq;

namespace ITAM.Tests.Services;

public class DisposalStatusServiceTests
{
    private readonly Mock<IDisposalStatusRepository> _repoMock = new();
    private readonly Mock<IAuditLogService> _auditMock = new();
    private readonly DisposalStatusService _sut;

    public DisposalStatusServiceTests()
    {
        _sut = new DisposalStatusService(_repoMock.Object, _auditMock.Object);
        _auditMock.Setup(a => a.RecordAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
    }

    private static UpsertDisposalStatusRequestDto Dto(string name = "Chờ chứng từ") =>
        new() { Name = name, Description = " mô tả ", Color = "warning", SortOrder = 10 };

    [Fact]
    public async Task CreateAsync_Valid_CreatesNonSystemStatusWithGeneratedCode()
    {
        DisposalStatus? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<DisposalStatus>()))
            .Callback<DisposalStatus>(s => { s.Id = 30; added = s; })
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(Dto("  Chờ chứng từ  "), currentUserId: 1);

        Assert.False(result.IsSystem);
        Assert.StartsWith("custom-", added!.Code);
        Assert.Equal("Chờ chứng từ", added.Name);
        Assert.Equal("mô tả", added.Description);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsConflict()
    {
        _repoMock.Setup(r => r.NameExistsAsync("Chờ chứng từ", null)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DisposalConflictException>(() => _sut.CreateAsync(Dto(), 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<DisposalStatus>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_SystemStatus_CanRenameButKeepsCodeAndSystemFlag()
    {
        var status = new DisposalStatus { Id = 2, Code = "Proposed", Name = "Đã đề xuất", Color = "warning", IsSystem = true };
        _repoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(status);

        var result = await _sut.UpdateAsync(2, Dto("Chờ duyệt"), 1);

        Assert.Equal("Chờ duyệt", status.Name);
        Assert.Equal("Proposed", status.Code);
        Assert.True(result.IsSystem);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((DisposalStatus?)null);

        await Assert.ThrowsAsync<DisposalStatusNotFoundException>(() => _sut.UpdateAsync(99, Dto(), 1));
    }

    [Fact]
    public async Task UpdateAsync_NameTakenByAnother_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new DisposalStatus { Id = 5, Code = "custom-x", Name = "A" });
        _repoMock.Setup(r => r.NameExistsAsync("Chờ chứng từ", 5)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DisposalConflictException>(() => _sut.UpdateAsync(5, Dto(), 1));
    }

    [Fact]
    public async Task DeleteAsync_MainStepNotInUse_IsAllowed()
    {
        var status = new DisposalStatus { Id = 1, Code = "Inspected", Name = "Đã kiểm tra", IsSystem = true };
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(status);
        _repoMock.Setup(r => r.IsInUseAsync(1)).ReturnsAsync(false);

        await _sut.DeleteAsync(1, 1);

        _repoMock.Verify(r => r.Remove(status), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_MainStepInUse_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(new DisposalStatus { Id = 2, Code = "Proposed", Name = "Đã đề xuất", IsSystem = true });
        _repoMock.Setup(r => r.IsInUseAsync(2)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DisposalConflictException>(() => _sut.DeleteAsync(2, 1));

        _repoMock.Verify(r => r.Remove(It.IsAny<DisposalStatus>()), Times.Never);
    }

    [Fact]
    public async Task RestoreDefaultsAsync_RecreatesOnlyMissingMainSteps()
    {
        // Chỉ thiếu "Rejected"; 4 bước còn lại vẫn tồn tại.
        _repoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => code == "Rejected" ? null : new DisposalStatus { Code = code, Name = code });
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DisposalStatus>());
        DisposalStatus? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<DisposalStatus>())).Callback<DisposalStatus>(s => added = s).Returns(Task.CompletedTask);

        await _sut.RestoreDefaultsAsync(1);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<DisposalStatus>()), Times.Once);
        Assert.Equal("Rejected", added!.Code);
        Assert.True(added.IsSystem);
        Assert.Equal("Từ chối", added.Name);
    }

    [Fact]
    public async Task RestoreDefaultsAsync_NameTakenByCustomStatus_AddsSuffix()
    {
        _repoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => code == "Rejected" ? null : new DisposalStatus { Code = code, Name = code });
        _repoMock.Setup(r => r.NameExistsAsync("Từ chối", null)).ReturnsAsync(true);
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DisposalStatus>());
        DisposalStatus? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<DisposalStatus>())).Callback<DisposalStatus>(s => added = s).Returns(Task.CompletedTask);

        await _sut.RestoreDefaultsAsync(1);

        Assert.Equal("Từ chối (mặc định)", added!.Name);
    }

    [Fact]
    public async Task DeleteAsync_CustomStatusInUse_ThrowsConflict()
    {
        _repoMock.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(new DisposalStatus { Id = 20, Code = "custom-1", Name = "X", IsSystem = false });
        _repoMock.Setup(r => r.IsInUseAsync(20)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DisposalConflictException>(() => _sut.DeleteAsync(20, 1));

        _repoMock.Verify(r => r.Remove(It.IsAny<DisposalStatus>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_UnusedCustomStatus_Removes()
    {
        var status = new DisposalStatus { Id = 20, Code = "custom-1", Name = "X", IsSystem = false };
        _repoMock.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(status);
        _repoMock.Setup(r => r.IsInUseAsync(20)).ReturnsAsync(false);

        await _sut.DeleteAsync(20, 1);

        _repoMock.Verify(r => r.Remove(status), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
