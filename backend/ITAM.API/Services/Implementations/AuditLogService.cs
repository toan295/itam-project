using System.Text.Json;
using ITAM.API.Models.DTOs.AuditLogs;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

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

    public async Task RecordAsync(
        int userId,
        string action,
        string entityName,
        int entityId,
        object? oldValue = null,
        object? newValue = null)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId,
            OldValue = SerializeValue(oldValue),
            NewValue = SerializeValue(newValue),
            Timestamp = DateTime.UtcNow,
        };

        await _repository.AddAsync(auditLog);
        await _repository.SaveChangesAsync();
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
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

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
        Action = auditLog.Action,
        EntityName = auditLog.EntityName,
        EntityId = auditLog.EntityId,
        OldValue = auditLog.OldValue,
        NewValue = auditLog.NewValue,
        Timestamp = auditLog.Timestamp,
    };
}
