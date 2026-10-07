using System.Text.Json;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Moq;

namespace ITAM.Tests.Services;

public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _repository = new();

    [Fact]
    public async Task RecordAsync_WithOldAndNewValue_SerializesBothAsJson()
    {
        AuditLog? captured = null;
        _repository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => captured = log)
            .Returns(Task.CompletedTask);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var service = CreateService();

        await service.RecordAsync(
            userId: 1,
            action: "Update",
            entityName: "User",
            entityId: 5,
            oldValue: new { FullName = "Old Name", IsActive = true },
            newValue: new { FullName = "New Name", IsActive = false });

        Assert.NotNull(captured);
        Assert.NotNull(captured!.OldValue);
        Assert.NotNull(captured.NewValue);

        using var oldJson = JsonDocument.Parse(captured.OldValue!);
        using var newJson = JsonDocument.Parse(captured.NewValue!);
        Assert.Equal("Old Name", oldJson.RootElement.GetProperty("fullName").GetString());
        Assert.True(oldJson.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal("New Name", newJson.RootElement.GetProperty("fullName").GetString());
        Assert.False(newJson.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task RecordAsync_CreateAction_OldValueIsNull()
    {
        AuditLog? captured = null;
        _repository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => captured = log)
            .Returns(Task.CompletedTask);
        _repository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var service = CreateService();

        await service.RecordAsync(
            userId: 1,
            action: "Create",
            entityName: "User",
            entityId: 10,
            oldValue: null,
            newValue: new { FullName = "New User" });

        Assert.NotNull(captured);
        Assert.Null(captured!.OldValue);
        Assert.NotNull(captured.NewValue);
        Assert.Equal("Create", captured.Action);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByEntityName_PassesFilterToRepository()
    {
        _repository
            .Setup(x => x.GetPagedAsync(1, 20, null, "User", null, null, null))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var service = CreateService();

        await service.GetPagedAsync(1, 20, entityName: "User");

        _repository.Verify(
            x => x.GetPagedAsync(1, 20, null, "User", null, null, null),
            Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByDateRange_PassesFilterToRepository()
    {
        var fromDate = new DateTime(2026, 9, 1);
        var toDate = new DateTime(2026, 9, 30);
        _repository
            .Setup(x => x.GetPagedAsync(1, 20, null, null, null, fromDate, toDate))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var service = CreateService();

        await service.GetPagedAsync(1, 20, fromDate: fromDate, toDate: toDate);

        _repository.Verify(
            x => x.GetPagedAsync(1, 20, null, null, null, fromDate, toDate),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsException()
    {
        _repository.Setup(x => x.GetByIdWithDetailsAsync(999)).ReturnsAsync((AuditLog?)null);
        var service = CreateService();

        await Assert.ThrowsAsync<AuditLogNotFoundException>(() => service.GetByIdAsync(999));
    }

    private AuditLogService CreateService() => new(_repository.Object);
}
