using System.Reflection;
using ITAM.API.Authorization;
using ITAM.API.Controllers;
using ITAM.API.Models.DTOs.Permissions;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ITAM.Tests.Services;

public class PermissionServiceTests
{
    private const int AdminId = 1, ManagerId = 2, TechnicianId = 3, EmployeeId = 4, Actor = 99;

    private readonly Mock<IRolePermissionRepository> _repo = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<IRolePermissionCache> _cache = new();
    private readonly Helpers.RecordingExclusiveSection _exclusive = new();
    private readonly PermissionService _sut;

    public PermissionServiceTests()
    {
        _roles.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Role>
        {
            new() { Id = AdminId, Name = "Admin IT" }, new() { Id = ManagerId, Name = "Manager" },
            new() { Id = TechnicianId, Name = "Technician" }, new() { Id = EmployeeId, Name = "Employee" },
        });
        _repo.Setup(r => r.GetAllByRoleIdAsync()).ReturnsAsync(new Dictionary<int, List<string>>
        {
            [ManagerId] = new() { Permissions.AssetsCreate, Permissions.MaintenanceAssign },
        });
        _repo.Setup(r => r.CountUsersByRoleAsync()).ReturnsAsync(new Dictionary<int, int> { [ManagerId] = 2 });
        _roles.Setup(r => r.GetIdByNameAsync("Admin IT")).ReturnsAsync(AdminId);
        _sut = new PermissionService(_repo.Object, _roles.Object, _audit.Object, _cache.Object, _exclusive);
    }

    // ----- Danh mục -----

    [Fact]
    public void Catalog_CodesAreUnique_AndDefaultsAreAllKnown()
    {
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.All.Select(p => p.Code).Distinct().Count());
        foreach (var role in new[] { "Admin IT", "Manager", "Technician", "Employee" })
        {
            Assert.All(PermissionCatalog.DefaultsFor(role), c => Assert.True(PermissionCatalog.IsKnown(c), c));
        }
    }

    [Fact]
    public void Catalog_SystemPermissions_AreOnlyInAdminDefaults()
    {
        foreach (var role in new[] { "Manager", "Technician", "Employee" })
        {
            Assert.DoesNotContain(PermissionCatalog.DefaultsFor(role), PermissionCatalog.IsSystemOnly);
        }

        Assert.All(PermissionCatalog.SystemCodes, c => Assert.Contains(c, PermissionCatalog.DefaultsFor("Admin IT")));
    }

    [Fact]
    public void Catalog_AdminDefaults_KeepDisposalSegregationOfDuties()
    {
        var admin = PermissionCatalog.DefaultsFor("Admin IT");
        Assert.DoesNotContain(Permissions.DisposalsInspect, admin);
        Assert.DoesNotContain(Permissions.DisposalsApprove, admin);
        Assert.Contains(Permissions.DisposalsExecute, admin);
    }

    [Fact]
    public void EveryHasPermissionOnControllers_UsesAKnownCode_AndEveryCodeIsUsed()
    {
        var used = typeof(PermissionsController).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<HasPermissionAttribute>(true)
                .Concat(t.GetMethods().SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>(true))))
            .Select(a => a.Permission)
            .ToHashSet();

        Assert.All(used, c => Assert.True(PermissionCatalog.IsKnown(c), $"Mã quyền chưa có trong danh mục: {c}"));
        // Quyền khai báo mà không endpoint nào dùng = ma trận hứa hẹn thứ không có thật.
        Assert.All(PermissionCatalog.All, p => Assert.Contains(p.Code, used));
    }

    // ----- Ma trận -----

    [Fact]
    public async Task GetMatrix_ReturnsGroupsAndAllRoles_WithUserCountsAndDefaults()
    {
        var m = await _sut.GetMatrixAsync();

        Assert.Equal(4, m.Roles.Count);
        Assert.Equal(PermissionCatalog.All.Count, m.Groups.Sum(g => g.Items.Count));
        var manager = m.Roles.Single(r => r.Name == "Manager");
        Assert.Equal(2, manager.UserCount);
        Assert.Equal(new[] { Permissions.AssetsCreate, Permissions.MaintenanceAssign }, manager.Permissions);
        Assert.Contains(Permissions.DisposalsApprove, manager.DefaultPermissions);
        Assert.True(m.Roles.Single(r => r.Name == "Employee").IsLocked);
        Assert.False(manager.IsLocked);
    }

    // ----- Cập nhật -----

    [Fact]
    public async Task UpdateRole_ValidCodes_ReplacesInvalidatesCacheAndAudits()
    {
        var result = await _sut.UpdateRoleAsync(ManagerId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.AssetsCreate, Permissions.LifecycleView, " " } }, Actor);

        _repo.Verify(r => r.ReplaceAsync(ManagerId, It.Is<IReadOnlyCollection<string>>(c =>
            c.SequenceEqual(new[] { Permissions.AssetsCreate, Permissions.LifecycleView }))), Times.Once);
        _cache.Verify(c => c.Invalidate(), Times.Once);
        _audit.Verify(a => a.RecordAsync(Actor, "UpdatePermissions", "Role", ManagerId, It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
        Assert.Equal(2, result.Permissions.Count);
    }

    [Fact]
    public async Task UpdateRole_UnknownCode_ThrowsAndDoesNotSave()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRoleAsync(ManagerId,
            new UpdateRolePermissionsRequestDto { Permissions = { "hack.everything" } }, Actor));

        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _cache.Verify(c => c.Invalidate(), Times.Never);
    }

    [Theory]
    [InlineData(ManagerId)]
    [InlineData(TechnicianId)]
    public async Task UpdateRole_SystemPermissionForNonAdmin_IsRejected(int roleId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRoleAsync(roleId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.UsersManage } }, Actor));

        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRole_AdminWithoutSystemPermissions_IsRejected_SoNobodyCanLockThemselvesOut()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRoleAsync(AdminId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.AssetsCreate } }, Actor));
    }

    [Fact]
    public async Task UpdateRole_AdminKeepingSystemPermissions_IsAllowed()
    {
        var codes = PermissionCatalog.SystemCodes.Append(Permissions.AssetsCreate).ToList();

        var result = await _sut.UpdateRoleAsync(AdminId, new UpdateRolePermissionsRequestDto { Permissions = codes }, Actor);

        Assert.Equal(codes.Count, result.Permissions.Count);
    }

    [Fact]
    public async Task UpdateRole_EmployeeIsLocked_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateRoleAsync(EmployeeId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.MaintenanceCreate } }, Actor));
    }

    [Fact]
    public async Task UpdateRole_UnknownRole_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.UpdateRoleAsync(
            777, new UpdateRolePermissionsRequestDto(), Actor));
    }

    [Fact]
    public async Task ResetRole_RestoresCatalogDefaults()
    {
        await _sut.ResetRoleAsync(TechnicianId, null, Actor);

        _repo.Verify(r => r.ReplaceAsync(TechnicianId, It.Is<IReadOnlyCollection<string>>(c =>
            c.ToHashSet().SetEquals(PermissionCatalog.DefaultsFor("Technician")))), Times.Once);
        _audit.Verify(a => a.RecordAsync(Actor, "ResetPermissions", "Role", TechnicianId, It.IsAny<object?>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task ResetRole_Employee_IsLocked()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetRoleAsync(EmployeeId, null, Actor));
    }

    // ----- Sửa đồng thời / revision -----

    private static string ManagerRevision() => PermissionRevision.Compute(new[] { Permissions.AssetsCreate, Permissions.MaintenanceAssign });

    [Fact]
    public async Task UpdateRole_NullPermissionsList_IsRejectedAsBadRequest_NotNullReference()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateRoleAsync(
            ManagerId, new UpdateRolePermissionsRequestDto { Permissions = null! }, Actor));
        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRole_StaleRevision_IsConflict_AndWritesNothing()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateRoleAsync(ManagerId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.AssetsCreate }, ExpectedRevision = "deadbeefdeadbeef" }, Actor));

        Assert.Contains("vừa được người khác thay đổi", ex.Message);
        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRole_CurrentRevision_Succeeds_AndRunsInsideTheAdminRoleLock()
    {
        await _sut.UpdateRoleAsync(ManagerId,
            new UpdateRolePermissionsRequestDto { Permissions = { Permissions.AssetsCreate }, ExpectedRevision = ManagerRevision() }, Actor);

        _repo.Verify(r => r.ReplaceAsync(ManagerId, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        Assert.Contains((LockTarget.Role, AdminId), _exclusive.Calls);
    }

    [Fact]
    public async Task ResetRole_StaleRevision_IsConflict()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetRoleAsync(
            ManagerId, new ResetRolePermissionsRequestDto { ExpectedRevision = "0000000000000000" }, Actor));
        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact]
    public void Revision_DependsOnlyOnTheSetOfCodes_NotTheirOrder()
    {
        Assert.Equal(PermissionRevision.Compute(new[] { "b", "a" }), PermissionRevision.Compute(new[] { "a", "b" }));
        Assert.NotEqual(PermissionRevision.Compute(new[] { "a" }), PermissionRevision.Compute(new[] { "a", "b" }));
    }

    // ----- Lưu cả ma trận một lần (nguyên tử) -----

    private static RolePermissionChangeDto Change(int roleId, string? revision, params string[] codes) =>
        new() { RoleId = roleId, ExpectedRevision = revision, Permissions = codes.ToList() };

    [Fact]
    public async Task UpdateMatrix_ValidChanges_WriteAllRolesInOneCall_AndAuditEach()
    {
        var techRevision = PermissionRevision.Compute(Array.Empty<string>());
        await _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(ManagerId, ManagerRevision(), Permissions.AssetsCreate), Change(TechnicianId, techRevision, Permissions.MaintenanceUpdateStatus) },
        }, Actor);

        _repo.Verify(r => r.ReplaceManyAsync(It.Is<IReadOnlyDictionary<int, IReadOnlyCollection<string>>>(d =>
            d.Count == 2 && d.ContainsKey(ManagerId) && d.ContainsKey(TechnicianId))), Times.Once);
        _repo.Verify(r => r.ReplaceAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(Actor, "UpdatePermissions", "Role", It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Exactly(2));
        _cache.Verify(c => c.Invalidate(), Times.Once);
        Assert.Contains((LockTarget.Role, AdminId), _exclusive.Calls);
    }

    [Fact]
    public async Task UpdateMatrix_OneRoleInvalid_NothingIsWritten_NotEvenTheValidRole()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles =
            {
                Change(ManagerId, ManagerRevision(), Permissions.AssetsCreate),
                Change(TechnicianId, PermissionRevision.Compute(Array.Empty<string>()), Permissions.UsersManage),
            },
        }, Actor));

        _repo.Verify(r => r.ReplaceManyAsync(It.IsAny<IReadOnlyDictionary<int, IReadOnlyCollection<string>>>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
        _cache.Verify(c => c.Invalidate(), Times.Never);
    }

    [Fact]
    public async Task UpdateMatrix_OneRoleStale_NothingIsWritten()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(ManagerId, ManagerRevision(), Permissions.AssetsCreate), Change(TechnicianId, "stale-revision-00", Permissions.MaintenanceUpdateStatus) },
        }, Actor));

        _repo.Verify(r => r.ReplaceManyAsync(It.IsAny<IReadOnlyDictionary<int, IReadOnlyCollection<string>>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateMatrix_MissingRevision_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(ManagerId, null, Permissions.AssetsCreate) },
        }, Actor));
    }

    [Fact]
    public async Task UpdateMatrix_DuplicateRoleOrEmptyRequest_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(ManagerId, ManagerRevision()), Change(ManagerId, ManagerRevision()) },
        }, Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto(), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto { Roles = null! }, Actor));
    }

    [Fact]
    public async Task UpdateMatrix_EmployeeRole_IsLocked_AndAdminMustKeepSystemCodes()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(EmployeeId, "x", Permissions.MaintenanceCreate) },
        }, Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(AdminId, PermissionRevision.Compute(Array.Empty<string>()), Permissions.AssetsCreate) },
        }, Actor));
    }

    [Fact]
    public async Task UpdateMatrix_NoActualChange_WritesAndAuditsNothing()
    {
        await _sut.UpdateMatrixAsync(new UpdatePermissionMatrixRequestDto
        {
            Roles = { Change(ManagerId, ManagerRevision(), Permissions.AssetsCreate, Permissions.MaintenanceAssign) },
        }, Actor);

        _repo.Verify(r => r.ReplaceManyAsync(It.IsAny<IReadOnlyDictionary<int, IReadOnlyCollection<string>>>()), Times.Never);
        _audit.Verify(a => a.RecordAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()), Times.Never);
    }

    // ----- Không có endpoint nào hở -----

    [Fact]
    public void EveryControllerAction_RequiresAuthentication_ExceptTheExplicitAnonymousOnes()
    {
        var anonymousAllowed = new HashSet<string> { "AuthController.Login", "AccountActivationController.GetInfo", "AccountActivationController.Activate" };
        var open = new List<string>();

        foreach (var type in typeof(PermissionsController).Assembly.GetTypes()
                     .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var classProtected = type.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(true).Any();
            var classAnonymous = type.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(true).Any();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>(true).Any()))
            {
                var methodProtected = method.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(true).Any();
                var methodAnonymous = method.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(true).Any();
                var anonymous = methodAnonymous || (classAnonymous && !methodProtected);
                var name = $"{type.Name}.{method.Name}";
                if (anonymous ? !anonymousAllowed.Contains(name) : !(classProtected || methodProtected))
                {
                    open.Add(name);
                }
            }
        }

        Assert.True(open.Count == 0, "Endpoint không được bảo vệ: " + string.Join(", ", open));
    }

    // ----- Bộ nhớ đệm / từ chối an toàn -----

    [Fact]
    public async Task PermissionHandler_CacheUnavailable_DeniesAndFlagsServiceUnavailable()
    {
        var cache = new Mock<IRolePermissionCache>();
        cache.Setup(c => c.HasAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("db down"));
        var handler = new PermissionHandler(cache.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionHandler>.Instance);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Manager") }, "test"));
        var requirement = new PermissionRequirement(Permissions.AssetsCreate);
        var ctx = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(new[] { requirement }, user, http);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
        Assert.True(http.Items.ContainsKey(PermissionHandler.SourceUnavailableItemKey));
    }

    [Fact]
    public async Task PermissionHandler_RoleWithoutPermission_IsDenied_AndUnknownRoleIsDenied()
    {
        var cache = new Mock<IRolePermissionCache>();
        cache.Setup(c => c.HasAsync("Manager", Permissions.UsersManage)).ReturnsAsync(false);
        var handler = new PermissionHandler(cache.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionHandler>.Instance);
        var requirement = new PermissionRequirement(Permissions.UsersManage);

        foreach (var role in new[] { "Manager", "Ghost" })
        {
            var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role) }, "test"));
            var ctx = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(new[] { requirement }, user, new Microsoft.AspNetCore.Http.DefaultHttpContext());
            await handler.HandleAsync(ctx);
            Assert.False(ctx.HasSucceeded, role);
        }
    }

    [Fact]
    public async Task RolePermissionCache_InvalidateDuringALoad_DoesNotCacheTheStaleSnapshot()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var repo = new Mock<IRolePermissionRepository>();
        repo.Setup(r => r.GetAllByRoleNameAsync()).Returns(async () =>
        {
            var n = Interlocked.Increment(ref calls);
            if (n == 1)
            {
                await gate.Task; // lần nạp đầu đọc dữ liệu cũ nhưng bị giữ lại
                return new Dictionary<string, HashSet<string>> { ["Manager"] = new() { Permissions.AssetsCreate } };
            }

            return new Dictionary<string, HashSet<string>> { ["Manager"] = new() };
        });
        using var provider = new ServiceCollection().AddScoped(_ => repo.Object).BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var cache = new RolePermissionCache(scopeFactory);

        var firstLoad = cache.HasAsync("Manager", Permissions.AssetsCreate); // bắt đầu nạp, đang chờ
        await Task.Delay(50);
        cache.Invalidate();                                                   // quyền vừa được sửa trong lúc đó
        gate.SetResult();
        await firstLoad;

        // Bản cũ không được lưu lại -> lần kiểm tra sau phải nạp lại và thấy quyền đã bị thu hồi.
        Assert.False(await cache.HasAsync("Manager", Permissions.AssetsCreate));
        Assert.Equal(2, calls);
    }
}
