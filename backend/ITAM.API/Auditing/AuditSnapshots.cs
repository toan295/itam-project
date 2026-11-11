using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITAM.API.Data;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Auditing;

/// <summary>Đọc bản ghi (trước/sau thao tác) bằng DbContext RIÊNG để không phụ thuộc/đụng vào trạng thái của request đang chạy.</summary>
public sealed class AuditSnapshotReader
{
    // Không bao giờ đưa vào nhật ký; "version" chỉ là bộ đếm kỹ thuật tăng mỗi lần sửa nên làm nhiễu phần so sánh.
    private static readonly Regex Sensitive = new("password|hash|token|secret", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase) { "Version" };

    private readonly IServiceScopeFactory _scopes;

    public AuditSnapshotReader(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<Dictionary<string, object?>?> ReadAsync(AuditTarget target, Func<string, string?> routeValue)
    {
        if (target.ClrType is null || target.Keys.Count == 0)
        {
            return null;
        }

        var keys = new List<(string Prop, int Value)>();
        foreach (var (prop, route) in target.Keys)
        {
            if (!int.TryParse(routeValue(route), out var v)) return null;
            keys.Add((prop, v));
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var method = typeof(AuditSnapshotReader).GetMethod(nameof(QueryAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(target.ClrType);
        var entity = await (Task<object?>)method.Invoke(null, new object[] { db, keys })!;
        if (entity is null) return null;

        var result = new Dictionary<string, object?>();
        foreach (var property in db.Model.FindEntityType(target.ClrType)!.GetProperties())
        {
            if (property.PropertyInfo is null || Sensitive.IsMatch(property.Name) || Noise.Contains(property.Name)) continue;
            result[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = Normalize(property.PropertyInfo.GetValue(entity));
        }

        return result;
    }

    // IgnoreQueryFilters: xoá mềm vẫn đọc được bản ghi để thấy cờ IsDeleted đổi.
    private static async Task<object?> QueryAsync<T>(AppDbContext db, List<(string Prop, int Value)> keys) where T : class
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking().IgnoreQueryFilters();
        foreach (var (prop, value) in keys)
        {
            var p = prop;
            var v = value;
            query = query.Where(e => EF.Property<int>(e, p) == v);
        }

        return await query.FirstOrDefaultAsync();
    }

    private static object? Normalize(object? value) => value switch
    {
        DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        Enum e => e.ToString(),
        _ => value,
    };
}

public static class AuditDiff
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Quy tắc giá trị cũ/mới: tạo mới -> cũ rỗng, mới = toàn bộ bản ghi; xoá cứng -> cũ = toàn bộ, mới rỗng;
    /// sửa -> chỉ các trường THỰC SỰ đổi (cũ và mới cùng danh sách trường); không đổi gì -> cả hai rỗng.
    /// </summary>
    public static (object? Old, object? New) Compute(Dictionary<string, object?>? before, Dictionary<string, object?>? after)
    {
        if (before is null && after is null) return (null, null);
        if (before is null) return (null, after);
        if (after is null) return (before, null);

        var oldChanged = new Dictionary<string, object?>();
        var newChanged = new Dictionary<string, object?>();
        foreach (var (key, newValue) in after)
        {
            before.TryGetValue(key, out var oldValue);
            if (JsonSerializer.Serialize(oldValue, Json) != JsonSerializer.Serialize(newValue, Json))
            {
                oldChanged[key] = oldValue;
                newChanged[key] = newValue;
            }
        }

        return oldChanged.Count == 0 ? (null, null) : (oldChanged, newChanged);
    }
}

public static class AuditRedactor
{
    private static readonly Regex Sensitive = new("password|hash|token|secret", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Chuyển đối tượng yêu cầu thành cấu trúc JSON đã che mọi trường nhạy cảm (mật khẩu, token...).</summary>
    public static object? Redact(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            p => p.Name,
            p => Sensitive.IsMatch(p.Name) ? "***" : Redact(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Redact).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}

/// <summary>Ghi một sự kiện bằng DbContext/scope RIÊNG: không bị ảnh hưởng nếu DbContext của request đang ở trạng thái lỗi/đã rollback.</summary>
public sealed class AuditEventWriter
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditEventWriter> _logger;

    public AuditEventWriter(IServiceScopeFactory scopes, ILogger<AuditEventWriter> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Không bao giờ ném lỗi: nhật ký hỏng không được làm hỏng thêm yêu cầu của người dùng (chỉ cảnh báo).</summary>
    public async Task WriteAsync(AuditEvent auditEvent)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            await service.RecordEventAsync(auditEvent);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không ghi được nhật ký {Action} {Entity}#{Id} ({Outcome})",
                auditEvent.Action, auditEvent.EntityName, auditEvent.EntityId, auditEvent.Outcome);
        }
    }
}
