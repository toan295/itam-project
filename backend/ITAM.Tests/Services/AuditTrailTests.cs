using System.Text.Json;
using ITAM.API.Auditing;
using ITAM.API.Filters;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITAM.Tests.Services;

public class AuditTrailTests
{
    // ----- Giá trị cũ / mới -----

    private static Dictionary<string, object?> Snap(string name, int qty) => new() { ["name"] = name, ["qty"] = qty, ["code"] = "A1" };

    [Fact]
    public void Diff_Update_KeepsOnlyChangedFields_OldAndNewShareTheSameKeys()
    {
        var (o, n) = AuditDiff.Compute(Snap("Cũ", 1), Snap("Mới", 1));

        var oldMap = Assert.IsType<Dictionary<string, object?>>(o);
        var newMap = Assert.IsType<Dictionary<string, object?>>(n);
        Assert.Equal(new[] { "name" }, oldMap.Keys);
        Assert.Equal("Cũ", oldMap["name"]);
        Assert.Equal("Mới", newMap["name"]);
    }

    [Fact]
    public void Diff_Create_OldIsEmpty_NewIsWholeRecord()
    {
        var (o, n) = AuditDiff.Compute(null, Snap("X", 2));
        Assert.Null(o);
        Assert.Equal(3, Assert.IsType<Dictionary<string, object?>>(n).Count);
    }

    [Fact]
    public void Diff_HardDelete_OldIsWholeRecord_NewIsEmpty()
    {
        var (o, n) = AuditDiff.Compute(Snap("X", 2), null);
        Assert.NotNull(o);
        Assert.Null(n);
    }

    [Fact]
    public void Diff_NothingChanged_BothEmpty()
    {
        var (o, n) = AuditDiff.Compute(Snap("X", 1), Snap("X", 1));
        Assert.Null(o);
        Assert.Null(n);
    }

    [Fact]
    public void Redactor_MasksPasswordsAndTokens_AtAnyDepth()
    {
        using var doc = JsonDocument.Parse("{\"currentPassword\":\"abc\",\"name\":\"An\",\"nested\":{\"resetToken\":\"t\",\"items\":[{\"secretKey\":\"s\",\"ok\":1}]}}");

        var text = JsonSerializer.Serialize(AuditRedactor.Redact(doc.RootElement));

        Assert.DoesNotContain("abc", text);
        Assert.DoesNotContain("\"t\"", text);
        Assert.DoesNotContain("\"s\"", text);
        Assert.Contains("\"name\":\"An\"", text);
        Assert.Contains("***", text);
    }

    // ----- Đối tượng/hành động suy ra từ request -----

    [Theory]
    [InlineData("Assets", "POST", new string[0], "Asset", "Create", false)]
    [InlineData("Assets", "PUT", new[] { "5" }, "Asset", "Update", false)]
    [InlineData("MaintenanceTickets", "PATCH", new[] { "5", "status" }, "MaintenanceTicket", "UpdateStatus", false)]
    [InlineData("SoftwareLicenses", "DELETE", new[] { "5", "assign", "9" }, "SoftwareLicense", "Unassign", false)]
    [InlineData("AssetAllocations", "GET", new[] { "5", "document" }, "AssetAllocation", "Print", false)]
    [InlineData("Users", "PUT", new[] { "5" }, "User", "Update", true)]
    [InlineData("Users", "POST", new[] { "5", "reset-password" }, "User", "ResetPassword", true)]
    [InlineData("DisposalRequests", "PATCH", new[] { "5", "sub-status" }, "DisposalRequest", "SetSubStatus", true)]
    [InlineData("DisposalStatuses", "POST", new[] { "restore-defaults" }, "DisposalStatus", "RestoreDefaults", true)]
    [InlineData("Permissions", "PUT", new[] { "matrix" }, "Role", "UpdatePermissions", true)]
    [InlineData("Permissions", "POST", new[] { "roles", "2", "reset" }, "Role", "ResetPermissions", true)]
    [InlineData("Auth", "POST", new[] { "change-password" }, "User", "ChangePassword", true)]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "POST", new[] { "generate" }, "BudgetForecast", "Generate", false)]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "DELETE", new[] { "5" }, "BudgetForecast", "Delete", false)]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "PUT", new[] { "reference-prices", "3" }, "ReferencePrice", "Upsert", false)]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "DELETE", new[] { "expansion-plans", "2027", "1" }, "ExpansionPlan", "Delete", false)]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "PUT", new[] { "device-ratios", "3" }, "DeviceRatio", "Upsert", false)]
    [InlineData("Lifecycle", "PUT", new[] { "policies", "3" }, "LifecyclePolicy", "Upsert", false)]
    public void Targets_MapEveryWriteEndpoint(string controller, string method, string[] segments, string entity, string action, bool serviceLogs)
    {
        var target = AuditTargets.Resolve(controller, method, segments);

        Assert.NotNull(target);
        Assert.Equal(entity, target!.Entity);
        Assert.Equal(action, target.Action);
        Assert.Equal(serviceLogs, target.ServiceLogsSuccess);
    }

    [Theory]
    [InlineData("Assets", "GET", new string[0])]
    [InlineData("Lifecycle", "GET", new[] { "stats" })]
    // [ĐÃ ĐÓNG] [InlineData("BudgetForecasts", "GET", new[] { "expansion-plans" })]
    [InlineData("AuditLogs", "POST", new string[0])]
    [InlineData("Auth", "POST", new[] { "login" })]
    public void Targets_ReadsAndUnlistedControllers_AreNotAudited(string controller, string method, string[] segments) =>
        Assert.Null(AuditTargets.Resolve(controller, method, segments));

    [Fact]
    public void AuditCatalog_CoversEveryEntityAndActionTheTargetsCanProduce()
    {
        var samples = new (string, string, string[])[]
        {
            ("Assets", "POST", Array.Empty<string>()), ("Departments", "PUT", new[] { "1" }), ("AssetCategories", "DELETE", new[] { "1" }),
            ("SoftwareLicenses", "POST", new[] { "1", "assign" }), ("MaintenanceTickets", "PATCH", new[] { "1", "priority" }),
            ("AssetAllocations", "POST", new[] { "1", "return" }), ("Employees", "DELETE", new[] { "1" }), ("Users", "DELETE", new[] { "1" }),
            ("DisposalRequests", "POST", new[] { "1", "approve" }), ("DisposalRequests", "POST", new[] { "1", "reject" }),
            ("DisposalRequests", "POST", new[] { "1", "complete" }), ("DisposalRequests", "POST", new[] { "1", "propose" }),
            ("Permissions", "PUT", new[] { "matrix" }),
            // [ĐÃ ĐÓNG] ("BudgetForecasts", "POST", new[] { "generate" }), ("BudgetForecasts", "PUT", new[] { "expansion-plans", "2027", "1" }), ("BudgetForecasts", "PUT", new[] { "device-ratios", "1" }),
            ("Lifecycle", "DELETE", new[] { "policies", "1" }), ("Auth", "POST", new[] { "change-password" }),
        };
        foreach (var (controller, method, segments) in samples)
        {
            var target = AuditTargets.Resolve(controller, method, segments)!;
            Assert.True(AuditCatalog.Entities.ContainsKey(target.Entity), $"thiếu đối tượng {target.Entity}");
            Assert.True(AuditCatalog.Actions.ContainsKey(target.Action), $"thiếu hành động {target.Action}");
        }
    }

    // ----- Phân loại kết quả -----

    private static ActionExecutedContext Executed(IActionResult? result, Exception? exception = null)
    {
        var ctx = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ActionExecutedContext(ctx, new List<IFilterMetadata>(), new object()) { Result = result, Exception = exception };
    }

    [Theory]
    [InlineData(200, "Success")]
    [InlineData(201, "Success")]
    [InlineData(400, "Rejected")]
    [InlineData(403, "Rejected")]
    [InlineData(404, "Rejected")]
    [InlineData(409, "Rejected")]
    [InlineData(500, "Failed")]
    [InlineData(503, "Failed")]
    public void Classify_ByStatusCode(int status, string expected)
    {
        var result = new ObjectResult(new ITAM.API.Models.DTOs.ApiResponse<object> { Message = "lý do" }) { StatusCode = status };

        var (outcome, code, message) = AuditActionFilter.Classify(Executed(result));

        Assert.Equal(expected, outcome);
        Assert.Equal(status, code);
        Assert.Equal(expected == "Success" ? null : "lý do", message);
    }

    [Fact]
    public void Classify_ValidationFailure_IncludesTheFirstErrors()
    {
        var response = ITAM.API.Models.DTOs.ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", new[] { "Tên không được để trống." });

        var (_, _, message) = AuditActionFilter.Classify(Executed(new BadRequestObjectResult(response)));

        Assert.Contains("Tên không được để trống.", message);
    }

    [Fact]
    public void Classify_UnhandledException_IsFailed_AndDoesNotLeakTheExceptionMessage()
    {
        var (outcome, status, message) = AuditActionFilter.Classify(Executed(null, new InvalidOperationException("chuỗi kết nối: Password=bí-mật")));

        Assert.Equal("Failed", outcome);
        Assert.Equal(500, status);
        Assert.DoesNotContain("bí-mật", message);
        Assert.Contains("InvalidOperationException", message);
    }

    [Fact]
    public void Classify_ConcurrencyException_IsRejectedConflict_NotFailed()
    {
        var (outcome, status, _) = AuditActionFilter.Classify(Executed(null, new DbUpdateConcurrencyException()));

        Assert.Equal("Rejected", outcome);
        Assert.Equal(409, status);
    }

    // ----- Dịch vụ nhật ký -----

    private readonly Mock<IAuditLogRepository> _repo = new();

    private AuditLogService Service() => new(_repo.Object, null, null);

    private AuditLog Capture(Func<AuditLogService, Task> act)
    {
        AuditLog? captured = null;
        _repo.Setup(r => r.Add(It.IsAny<AuditLog>())).Callback<AuditLog>(l => captured = l);
        act(Service()).GetAwaiter().GetResult();
        return captured!;
    }

    [Fact]
    public void RecordEvent_StoresOutcomeStatusMessageAndDetail_AndSnapshotsTheActor()
    {
        _repo.Setup(r => r.GetActorSnapshotAsync(7)).ReturnsAsync(new AuditActorSnapshot("Nguyễn A", "Manager"));

        var log = Capture(s => s.RecordEventAsync(new AuditEvent(7, "Create", "AssetAllocation", 0, "Rejected", null, null, 409, "Tài sản đã được cấp.", new { employeeId = 3 })));

        Assert.Equal("Rejected", log.Outcome);
        Assert.Equal(409, log.StatusCode);
        Assert.Equal("Tài sản đã được cấp.", log.Message);
        Assert.Contains("employeeId", log.Detail);
        Assert.Equal("Nguyễn A", log.ActorName);
        Assert.Equal("Manager", log.ActorRole);
        Assert.Null(log.OldValue);
        Assert.Null(log.NewValue);
    }

    [Fact]
    public async Task RecordEvent_InvalidOutcome_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().RecordEventAsync(new AuditEvent(1, "Create", "Asset", 1, "Maybe")));
        _repo.Verify(r => r.Add(It.IsAny<AuditLog>()), Times.Never);
    }

    private static AuditLog Copy(AuditLog l) => new()
    {
        Id = l.Id, UserId = l.UserId, Action = l.Action, EntityName = l.EntityName, EntityId = l.EntityId, OldValue = l.OldValue,
        NewValue = l.NewValue, Timestamp = l.Timestamp, Outcome = l.Outcome, StatusCode = l.StatusCode, Message = l.Message,
        Detail = l.Detail, RequestId = l.RequestId, IpAddress = l.IpAddress, ActorName = l.ActorName, ActorRole = l.ActorRole,
        RowHash = l.RowHash, User = new User { FullName = "A" },
    };

    [Fact]
    public async Task RecordEvent_SignsTheRow_AndDetailEndpointDetectsAnyFieldEdit()
    {
        var log = Capture(s => s.RecordAsync(1, "Update", "Asset", 5, new { name = "a" }, new { name = "b" }));
        Assert.False(string.IsNullOrEmpty(log.RowHash));

        _repo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(Copy(log));
        Assert.Equal("Valid", (await Service().GetByIdAsync(1)).Integrity);

        var tampers = new Action<AuditLog>[]
        {
            l => l.NewValue = "{\"name\":\"hack\"}", l => l.Outcome = "Rejected", l => l.Message = "x", l => l.EntityId = 6,
            l => l.UserId = 2, l => l.Timestamp = l.Timestamp.AddSeconds(1), l => l.Action = "Delete",
        };
        foreach (var tamper in tampers)
        {
            var copy = Copy(log);
            tamper(copy);
            _repo.Setup(r => r.GetByIdWithDetailsAsync(2)).ReturnsAsync(copy);
            Assert.Equal("Tampered", (await Service().GetByIdAsync(2)).Integrity);
        }
    }

    [Fact]
    public async Task GetById_LegacyRowWithoutHash_IsReportedUnsigned_NotTampered()
    {
        _repo.Setup(r => r.GetByIdWithDetailsAsync(9)).ReturnsAsync(new AuditLog { Id = 9, Action = "Update", EntityName = "Asset", RowHash = null, User = new User { FullName = "A" } });

        Assert.Equal("Unsigned", (await Service().GetByIdAsync(9)).Integrity);
    }

    [Fact]
    public async Task GetPaged_DatesAreVietnamCalendarDays_ConvertedToUtcRange()
    {
        _repo.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), null, null, null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<AuditLogListExtras>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        // Chọn 29/10 -> [28/10 17:00 UTC, 29/10 17:00 UTC): bản ghi 23:30 giờ VN ngày 29/10 (16:30 UTC) vẫn thuộc ngày 29/10.
        await Service().GetPagedAsync(1, 20, fromDate: new DateTime(2026, 10, 29), toDate: new DateTime(2026, 10, 29));

        _repo.Verify(r => r.GetPagedAsync(1, 20, null, null, null,
            new DateTime(2026, 10, 28, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 29, 17, 0, 0, DateTimeKind.Utc),
            It.IsAny<AuditLogListExtras>()), Times.Once);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(-1, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, -3, null)]
    [InlineData(null, null, "Hack")]
    public async Task GetPaged_InvalidIdsOrOutcome_Throw(int? userId, int? entityId, string? outcome)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().GetPagedAsync(1, 20, userId: userId, entityId: entityId, outcome: outcome));
    }

    [Fact]
    public async Task Actors_DeletedAccountEmail_IsNeverExposed()
    {
        _repo.Setup(r => r.GetActorsAsync()).ReturnsAsync(new List<AuditActorRow>
        {
            new(1, "Admin", "admin@x.vn", false, "Admin IT", 5),
            new(2, "Cựu NV", "deleted.2.20261028@deleted.invalid", true, "Technician", 2),
        });

        var actors = await Service().GetActorsAsync();

        Assert.Equal("admin@x.vn", actors[0].Email);
        Assert.Null(actors[1].Email);
        Assert.True(actors[1].IsDeleted);
    }

    [Fact]
    public async Task Catalog_ListsEverySystemEntity_PlusUnknownCodesFoundInData_WithCounts()
    {
        _repo.Setup(r => r.CountByAsync("EntityName")).ReturnsAsync(new Dictionary<string, int> { ["Asset"] = 4, ["LegacyThing"] = 2 });
        _repo.Setup(r => r.CountByAsync("Action")).ReturnsAsync(new Dictionary<string, int>());
        _repo.Setup(r => r.CountByAsync("Outcome")).ReturnsAsync(new Dictionary<string, int> { ["Rejected"] = 1 });

        var catalog = await Service().GetCatalogAsync();

        // [ĐÃ ĐÓNG] Assert.Contains(catalog.Entities, e => e.Code == "ExpansionPlan" && e.Count == 0 && e.Known);
        Assert.Contains(catalog.Entities, e => e.Code == "LifecyclePolicy" && e.Count == 0 && e.Known);
        Assert.Contains(catalog.Entities, e => e.Code == "Asset" && e.Count == 4);
        Assert.Contains(catalog.Entities, e => e.Code == "LegacyThing" && e.Count == 2 && !e.Known);
        Assert.Equal(3, catalog.Outcomes.Count);
    }

    [Fact]
    public async Task VerifyIntegrity_CountsValidUnsignedAndTampered()
    {
        var good = Capture(s => s.RecordAsync(1, "Create", "Asset", 1));
        good.Id = 1;
        var bad = new AuditLog { Id = 2, Action = "Create", EntityName = "Asset", EntityId = 1, Timestamp = good.Timestamp, RowHash = "DEADBEEF" };
        var legacy = new AuditLog { Id = 3, Action = "Create", EntityName = "Asset", RowHash = null };
        _repo.Setup(r => r.ImmutabilityTriggersPresentAsync()).ReturnsAsync(true);
        _repo.Setup(r => r.GetBatchAsync(0, It.IsAny<int>())).ReturnsAsync(new List<AuditLog> { good, bad, legacy });
        _repo.Setup(r => r.GetBatchAsync(3, It.IsAny<int>())).ReturnsAsync(new List<AuditLog>());

        var report = await Service().VerifyIntegrityAsync();

        Assert.Equal(3, report.Checked);
        Assert.Equal(1, report.Valid);
        Assert.Equal(1, report.Unsigned);
        Assert.Equal(new long[] { 2 }, report.TamperedIds);
        Assert.True(report.ImmutabilityTriggersPresent);
    }
}
