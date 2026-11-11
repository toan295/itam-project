using ITAM.API.Repositories.Models;
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
            .Setup(x => x.Add(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => captured = log);

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
            .Setup(x => x.Add(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(log => captured = log);

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
            .Setup(x => x.GetPagedAsync(1, 20, null, "User", null, null, null, It.IsAny<AuditLogListExtras>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var service = CreateService();

        await service.GetPagedAsync(1, 20, entityName: "User");

        _repository.Verify(
            x => x.GetPagedAsync(1, 20, null, "User", null, null, null, It.IsAny<AuditLogListExtras>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByDateRange_PassesFilterToRepository()
    {
        var fromDate = new DateTime(2026, 9, 1);
        var toDate = new DateTime(2026, 9, 30);
        // Ngày lọc là ngày lịch Việt Nam (UTC+7): 01/09 00:00 VN = 31/08 17:00 UTC; hết 30/09 VN = 30/09 17:00 UTC (không bao gồm).
        var fromUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc);
        var toUtcExclusive = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
        _repository
            .Setup(x => x.GetPagedAsync(1, 20, null, null, null, fromUtc, toUtcExclusive, It.IsAny<AuditLogListExtras>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var service = CreateService();

        await service.GetPagedAsync(1, 20, fromDate: fromDate, toDate: toDate);

        _repository.Verify(
            x => x.GetPagedAsync(1, 20, null, null, null, fromUtc, toUtcExclusive, It.IsAny<AuditLogListExtras>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsException()
    {
        _repository.Setup(x => x.GetByIdWithDetailsAsync(999)).ReturnsAsync((AuditLog?)null);
        var service = CreateService();

        await Assert.ThrowsAsync<AuditLogNotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task RecordAsync_OnlyAddsToRepository_DoesNotSave()
    {
        var service = CreateService();

        await service.RecordAsync(1, "Update", "User", 5, null, new { A = 1 });

        _repository.Verify(x => x.Add(It.Is<AuditLog>(l => l.UserId == 1 && l.EntityId == 5 && l.Outcome == "Success")), Times.Once);
        _repository.Verify(x => x.GetActorSnapshotAsync(1), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", "User")]
    [InlineData("Update", "  ")]
    public async Task RecordAsync_BlankActionOrEntity_Throws(string action, string entity)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(1, action, entity, 1));
        _repository.Verify(x => x.Add(It.IsAny<AuditLog>()), Times.Never);
    }

    [Fact]
    public async Task RecordAsync_ActionTooLong_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAsync(1, new string('a', 51), "User", 1));
    }

    [Fact]
    public async Task GetPagedAsync_KeywordEntityIdAndSort_AreMappedToExtras()
    {
        _repository
            .Setup(x => x.GetPagedAsync(1, 20, null, null, null, null, null, It.IsAny<AuditLogListExtras>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        var service = CreateService();

        await service.GetPagedAsync(1, 20, keyword: "laptop", entityId: 12, sort: "oldest");

        _repository.Verify(
            x => x.GetPagedAsync(1, 20, null, null, null, null, null,
                It.Is<AuditLogListExtras>(e => e.Keyword == "laptop" && e.EntityId == 12 && e.Sort == "oldest")),
            Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidSort_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPagedAsync(1, 20, sort: "random"));
    }

    [Fact]
    public async Task GetPagedAsync_FromDateAfterToDate_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPagedAsync(1, 20, fromDate: new DateTime(2026, 10, 5), toDate: new DateTime(2026, 10, 1)));
    }

    [Fact]
    public async Task GetByIdAsync_Found_MapsUserName()
    {
        _repository.Setup(x => x.GetByIdWithDetailsAsync(3)).ReturnsAsync(new AuditLog
        {
            Id = 3, UserId = 7, Action = "Create", EntityName = "User", EntityId = 9,
            User = new User { Id = 7, FullName = "Admin A" },
        });
        var service = CreateService();

        var dto = await service.GetByIdAsync(3);

        Assert.Equal("Admin A", dto.UserName);
    }

    private AuditLogService CreateService() => new(_repository.Object);
}
