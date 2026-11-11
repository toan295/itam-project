using System.Security.Claims;
using ITAM.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace ITAM.API.Authorization;

/// <summary>Thay cho [Authorize(Roles = "...")]: yêu cầu vai trò của người dùng đang được cấp quyền này trong ma trận phân quyền.</summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission) : base(PolicyPrefix + permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    public string Permission { get; }
}

/// <summary>Dựng policy "perm:xxx" theo yêu cầu — không cần đăng ký từng policy.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[HasPermissionAttribute.PolicyPrefix.Length..]))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    public const string SourceUnavailableItemKey = "permission-source-unavailable";

    private readonly IRolePermissionCache _cache;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(IRolePermissionCache cache, ILogger<PermissionHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        try
        {
            if (role is not null && await _cache.HasAsync(role, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Không đọc được nguồn quyền -> TỪ CHỐI (không bao giờ mặc định cho qua). Đánh dấu để trả 503 thay vì 403,
            // vì đây là lỗi hệ thống chứ không phải người dùng thiếu quyền.
            _logger.LogError(ex, "Không đọc được ma trận quyền; từ chối yêu cầu theo nguyên tắc an toàn.");
            if (context.Resource is HttpContext http)
            {
                http.Items[SourceUnavailableItemKey] = true;
            }
        }
    }
}

public interface IRolePermissionCache
{
    Task<bool> HasAsync(string roleName, string permission);
    Task<IReadOnlySet<string>> GetAsync(string roleName);
    /// <summary>Gọi sau khi sửa ma trận để mọi request sau thấy ngay (trên instance này).</summary>
    void Invalidate();
}

/// <summary>
/// Bộ nhớ đệm ma trận quyền (vai trò → tập mã quyền). Đọc DB một lần cho cả bảng, làm mới khi hết TTL hoặc khi Invalidate().
/// TTL ngắn (mặc định 10 giây, cấu hình Authorization:PermissionCacheSeconds) để nhiều instance API cũng đồng bộ: quyền bị thu hồi
/// có hiệu lực ngay trên instance đã lưu, và trên các instance khác sau tối đa bằng TTL. Không tìm thấy vai trò → không có quyền (an toàn).
/// </summary>
public sealed class RolePermissionCache : IRolePermissionCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _ttl;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, HashSet<string>>? _snapshot;
    private DateTime _loadedAtUtc;
    private int _generation; // tăng mỗi lần Invalidate: lần nạp bắt đầu trước đó không được ghi đè bộ nhớ đệm bằng dữ liệu cũ.

    public RolePermissionCache(IServiceScopeFactory scopeFactory, TimeSpan? ttl = null)
    {
        _scopeFactory = scopeFactory;
        _ttl = ttl ?? DefaultTtl;
    }

    public async Task<bool> HasAsync(string roleName, string permission) =>
        (await GetAsync(roleName)).Contains(permission);

    public async Task<IReadOnlySet<string>> GetAsync(string roleName)
    {
        var snapshot = await GetSnapshotAsync();
        return snapshot.TryGetValue(roleName, out var set) ? set : new HashSet<string>();
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _snapshot = null;
    }

    private async Task<Dictionary<string, HashSet<string>>> GetSnapshotAsync()
    {
        var current = _snapshot;
        if (current is not null && DateTime.UtcNow - _loadedAtUtc < _ttl)
        {
            return current;
        }

        await _lock.WaitAsync();
        try
        {
            if (_snapshot is not null && DateTime.UtcNow - _loadedAtUtc < _ttl)
            {
                return _snapshot;
            }

            var generation = Volatile.Read(ref _generation);
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>();
            var loaded = await repo.GetAllByRoleNameAsync();
            if (generation == Volatile.Read(ref _generation))
            {
                _snapshot = loaded;
                _loadedAtUtc = DateTime.UtcNow;
            }

            return loaded;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Trả 503 (thử lại sau) khi bị từ chối vì không đọc được nguồn quyền; mọi trường hợp khác dùng xử lý mặc định (401/403).</summary>
public sealed class PermissionSourceAwareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded && context.Items.ContainsKey(PermissionHandler.SourceUnavailableItemKey))
        {
            await Auditing.AuditDenied.RecordAsync(context, StatusCodes.Status503ServiceUnavailable,
                "Không kiểm tra được quyền do lỗi hệ thống nên yêu cầu bị từ chối.", Models.Entities.AuditOutcome.Failed);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers["Retry-After"] = "5";
            await context.Response.WriteAsJsonAsync(
                ITAM.API.Models.DTOs.ApiResponse<object>.Fail("Không kiểm tra được quyền do lỗi hệ thống. Vui lòng thử lại sau."));
            return;
        }

        if (authorizeResult.Forbidden)
        {
            // Người đã đăng nhập nhưng không đủ quyền: yêu cầu ghi bị từ chối, không có thay đổi dữ liệu.
            await Auditing.AuditDenied.RecordAsync(context, StatusCodes.Status403Forbidden, "Không đủ quyền thực hiện thao tác này.");
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
