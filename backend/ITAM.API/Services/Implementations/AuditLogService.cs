using ITAM.API.Repositories.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITAM.API.Auditing;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.AuditLogs;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private const int MaxActionLength = 50;
    private const int MaxEntityNameLength = 100;
    private const int MaxMessageLength = 500;
    private const int VerifyBatchSize = 1000;
    private const int VerifyMaxRows = 200_000;
    private const int MaxTamperedIdsReported = 100;

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAuditLogRepository _repository;
    private readonly IHttpContextAccessor? _http;
    private readonly byte[] _integrityKey;

    public AuditLogService(IAuditLogRepository repository, IHttpContextAccessor? http = null, IConfiguration? configuration = null)
    {
        _repository = repository;
        _http = http;
        // Khoá ký riêng (AuditLog:IntegrityKey) nên đặt ở biến môi trường/secret manager; mặc định suy ra từ khoá JWT (đổi khoá JWT sẽ làm
        // chữ ký cũ không còn kiểm tra được -> cấu hình khoá riêng ở môi trường thật).
        var secret = configuration?["AuditLog:IntegrityKey"] ?? configuration?["JwtSettings:Secret"] ?? "eaims-audit-test-key";
        _integrityKey = SHA256.HashData(Encoding.UTF8.GetBytes("eaims-audit-integrity-v1|" + secret));
    }

    // KHÔNG SaveChangesAsync ở đây (cố ý): nơi gọi đã có SaveChangesAsync cho nghiệp vụ của nó; lưu riêng
    // sẽ tách thành 2 transaction (mất atomic) và còn vô tình commit luôn mọi thay đổi đang chờ trong
    // DbContext dùng chung.
    public Task RecordAsync(
        int userId,
        string action,
        string entityName,
        int entityId,
        object? oldValue = null,
        object? newValue = null) =>
        RecordEventAsync(new AuditEvent(userId, action, entityName, entityId, AuditOutcome.Success, oldValue, newValue));

    public async Task RecordEventAsync(AuditEvent e)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(e.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(e.EntityName);
        var action = e.Action.Trim();
        var entityName = e.EntityName.Trim();
        // Khớp độ dài cột (AppDbContext): báo lỗi rõ ngay thay vì để DB từ chối lúc nơi gọi SaveChanges.
        if (action.Length > MaxActionLength || entityName.Length > MaxEntityNameLength)
        {
            throw new ArgumentException(
                $"Action tối đa {MaxActionLength} ký tự, EntityName tối đa {MaxEntityNameLength} ký tự.");
        }

        if (!AuditOutcome.All.Contains(e.Outcome))
        {
            throw new ArgumentException($"Outcome không hợp lệ (chấp nhận: {string.Join(", ", AuditOutcome.All)}).");
        }

        var actor = await _repository.GetActorSnapshotAsync(e.UserId);
        var http = _http?.HttpContext;
        var message = e.Message?.Trim();

        var auditLog = new AuditLog
        {
            UserId = e.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = e.EntityId,
            Outcome = e.Outcome,
            StatusCode = e.StatusCode,
            Message = message is { Length: > MaxMessageLength } ? message[..MaxMessageLength] : message,
            OldValue = SerializeValue(e.OldValue),
            NewValue = SerializeValue(e.NewValue),
            Detail = SerializeValue(e.Detail),
            RequestId = http?.TraceIdentifier is { Length: > 0 } trace ? (trace.Length > 64 ? trace[..64] : trace) : null,
            IpAddress = http?.Connection.RemoteIpAddress is { } ip ? ip.MapToIPv6().IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString() : null,
            ActorName = actor?.Name,
            ActorRole = actor?.Role,
            Timestamp = TruncateToMicroseconds(DateTime.UtcNow),
        };
        auditLog.RowHash = ComputeHash(auditLog);

        _repository.Add(auditLog);
    }

    public async Task<PagedResultDto<AuditLogResponseDto>> GetPagedAsync(
        int page,
        int pageSize,
        int? userId = null,
        string? entityName = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? keyword = null,
        int? entityId = null,
        string? sort = null,
        string? outcome = null)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        if (sort is not null && sort is not ("newest" or "oldest"))
        {
            throw new ArgumentException("sort không hợp lệ. Phải là một trong: newest, oldest");
        }

        if (userId is <= 0)
        {
            throw new ArgumentException("userId không hợp lệ.");
        }

        if (entityId is <= 0)
        {
            throw new ArgumentException("entityId phải là số nguyên dương.");
        }

        if (!string.IsNullOrWhiteSpace(outcome) && !AuditOutcome.All.Contains(outcome.Trim()))
        {
            throw new ArgumentException($"outcome không hợp lệ (chấp nhận: {string.Join(", ", AuditOutcome.All)}).");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);

        // Ngày lọc là NGÀY LỊCH VIỆT NAM (người dùng chọn 29/10 là 00:00 29/10 đến hết 29/10 giờ VN), còn thời điểm ghi lưu theo UTC.
        var (items, totalItems) = await _repository.GetPagedAsync(
            page,
            pageSize,
            userId,
            entityName,
            action,
            fromDate.HasValue ? StartOfVietnamDayUtc(fromDate.Value) : null,
            toDate.HasValue ? StartOfVietnamDayUtc(toDate.Value.AddDays(1)) : null,
            new AuditLogListExtras { Keyword = keyword, EntityId = entityId, Sort = sort, Outcome = outcome?.Trim() });

        return new PagedResultDto<AuditLogResponseDto>
        {
            Items = items.Select(l => MapToDto(l)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AuditLogResponseDto> GetByIdAsync(long id)
    {
        var auditLog = await _repository.GetByIdWithDetailsAsync(id)
            ?? throw new AuditLogNotFoundException(id);

        var dto = MapToDto(auditLog);
        dto.Integrity = auditLog.RowHash is null ? "Unsigned" : IsHashValid(auditLog) ? "Valid" : "Tampered";
        return dto;
    }

    public async Task<List<AuditActorDto>> GetActorsAsync() =>
        (await _repository.GetActorsAsync()).Select(a => new AuditActorDto
        {
            Id = a.Id,
            Name = a.Name,
            // Tài khoản đã xoá: email đã bị thay bằng giá trị vô hiệu -> không hiển thị.
            Email = a.IsDeleted ? null : a.Email,
            IsDeleted = a.IsDeleted,
            Role = a.Role,
            EventCount = a.EventCount,
        }).ToList();

    public async Task<AuditCatalogDto> GetCatalogAsync()
    {
        var entityCounts = await _repository.CountByAsync("EntityName");
        var actionCounts = await _repository.CountByAsync("Action");
        var outcomeCounts = await _repository.CountByAsync("Outcome");

        return new AuditCatalogDto
        {
            Entities = Merge(AuditCatalog.Entities, entityCounts),
            Actions = Merge(AuditCatalog.Actions, actionCounts),
            Outcomes = Merge(AuditCatalog.Outcomes, outcomeCounts),
        };
    }

    public async Task<AuditIntegrityReportDto> VerifyIntegrityAsync()
    {
        var report = new AuditIntegrityReportDto { ImmutabilityTriggersPresent = await _repository.ImmutabilityTriggersPresentAsync() };
        long lastId = 0;
        while (report.Checked < VerifyMaxRows)
        {
            var batch = await _repository.GetBatchAsync(lastId, VerifyBatchSize);
            if (batch.Count == 0)
            {
                return report;
            }

            foreach (var log in batch)
            {
                report.Checked++;
                if (log.RowHash is null)
                {
                    report.Unsigned++;
                }
                else if (IsHashValid(log))
                {
                    report.Valid++;
                }
                else if (report.TamperedIds.Count < MaxTamperedIdsReported)
                {
                    report.TamperedIds.Add(log.Id);
                }
            }

            lastId = batch[^1].Id;
        }

        report.Truncated = true;
        return report;
    }

    public static DateTime StartOfVietnamDayUtc(DateTime date) => DateTime.SpecifyKind(date.Date.AddHours(-7), DateTimeKind.Utc);

    // MySQL datetime(6) giữ tới micro-giây: cắt sẵn để chữ ký tính lúc ghi trùng với giá trị đọc lại từ DB.
    private static DateTime TruncateToMicroseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    private string ComputeHash(AuditLog log)
    {
        var canonical = string.Join('\u001f',
            log.UserId, log.Action, log.EntityName, log.EntityId, log.Outcome, log.StatusCode, log.Message, log.OldValue, log.NewValue,
            log.Detail, log.RequestId, log.IpAddress, log.ActorName, log.ActorRole,
            log.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"));
        return Convert.ToHexString(HMACSHA256.HashData(_integrityKey, Encoding.UTF8.GetBytes(canonical)));
    }

    private bool IsHashValid(AuditLog log) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(ComputeHash(log)), Encoding.ASCII.GetBytes(log.RowHash ?? string.Empty));

    private static List<AuditCatalogItemDto> Merge(IReadOnlyDictionary<string, string> known, Dictionary<string, int> counts)
    {
        var items = known.Select(k => new AuditCatalogItemDto { Code = k.Key, Label = k.Value, Count = counts.GetValueOrDefault(k.Key) }).ToList();
        items.AddRange(counts.Where(c => !known.ContainsKey(c.Key))
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => new AuditCatalogItemDto { Code = c.Key, Label = c.Key, Count = c.Value, Known = false }));
        return items;
    }

    private static string? SerializeValue(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, AuditJsonOptions);

    private static AuditLogResponseDto MapToDto(AuditLog auditLog) => new()
    {
        Id = auditLog.Id,
        UserId = auditLog.UserId,
        UserName = auditLog.ActorName ?? auditLog.User?.FullName ?? string.Empty,
        ActorRole = auditLog.ActorRole,
        UserEmail = auditLog.User is { IsDeleted: false } ? auditLog.User.Email : null,
        UserIsDeleted = auditLog.User?.IsDeleted ?? false,
        Action = auditLog.Action,
        EntityName = auditLog.EntityName,
        EntityId = auditLog.EntityId,
        Outcome = auditLog.Outcome,
        StatusCode = auditLog.StatusCode,
        Message = auditLog.Message,
        OldValue = auditLog.OldValue,
        NewValue = auditLog.NewValue,
        Detail = auditLog.Detail,
        RequestId = auditLog.RequestId,
        IpAddress = auditLog.IpAddress,
        Timestamp = auditLog.Timestamp,
    };
}
