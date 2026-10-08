using System.Text.Json;
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

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    // KHÔNG SaveChangesAsync ở đây (cố ý): nơi gọi đã có SaveChangesAsync cho nghiệp vụ của nó; lưu riêng
    // sẽ tách thành 2 transaction (mất atomic) và còn vô tình commit luôn mọi thay đổi đang chờ trong
    // DbContext dùng chung. "Async" chỉ để giữ chữ ký Task nhất quán.
    public Task RecordAsync(
        int userId,
        string action,
        string entityName,
        int entityId,
        object? oldValue = null,
        object? newValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        action = action.Trim();
        entityName = entityName.Trim();
        // Khớp độ dài cột (AppDbContext): báo lỗi rõ ngay thay vì để DB từ chối lúc nơi gọi SaveChanges.
        if (action.Length > MaxActionLength || entityName.Length > MaxEntityNameLength)
        {
            throw new ArgumentException(
                $"Action tối đa {MaxActionLength} ký tự, EntityName tối đa {MaxEntityNameLength} ký tự.");
        }

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValue = SerializeValue(oldValue),
            NewValue = SerializeValue(newValue),
            Timestamp = DateTime.UtcNow,
        };

        _repository.Add(auditLog);
        return Task.CompletedTask;
    }

    public async Task<PagedResultDto<AuditLogResponseDto>> GetPagedAsync(
        int page,
        int pageSize,
        int? userId = null,
        string? entityName = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, totalItems) = await _repository.GetPagedAsync(
            page,
            pageSize,
            userId,
            entityName,
            action,
            fromDate,
            toDate);

        return new PagedResultDto<AuditLogResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AuditLogResponseDto> GetByIdAsync(long id)
    {
        var auditLog = await _repository.GetByIdWithDetailsAsync(id)
            ?? throw new AuditLogNotFoundException(id);

        return MapToDto(auditLog);
    }

    private static string? SerializeValue(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, AuditJsonOptions);

    private static AuditLogResponseDto MapToDto(AuditLog auditLog) => new()
    {
        Id = auditLog.Id,
        UserId = auditLog.UserId,
        UserName = auditLog.User?.FullName ?? string.Empty,
        Action = auditLog.Action,
        EntityName = auditLog.EntityName,
        EntityId = auditLog.EntityId,
        OldValue = auditLog.OldValue,
        NewValue = auditLog.NewValue,
        Timestamp = auditLog.Timestamp,
    };
}
