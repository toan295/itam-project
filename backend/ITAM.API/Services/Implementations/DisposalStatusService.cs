using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class DisposalStatusService : IDisposalStatusService
{
    private readonly IDisposalStatusRepository _repo;
    private readonly IAuditLogService _auditLogService;

    public DisposalStatusService(IDisposalStatusRepository repo, IAuditLogService auditLogService)
    {
        _repo = repo;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<DisposalStatusDto>> GetAllAsync() =>
        (await _repo.GetAllAsync()).Select(MapToDto).ToList();

    public async Task<DisposalStatusDto> CreateAsync(UpsertDisposalStatusRequestDto dto, int currentUserId)
    {
        var name = dto.Name.Trim();
        await EnsureNameAvailableAsync(name, excludeId: null);

        var status = new DisposalStatus
        {
            // Code sinh tự động, ổn định — không phụ thuộc tên (tên đổi được, code thì không).
            Code = "custom-" + Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Color = dto.Color,
            SortOrder = dto.SortOrder,
            IsSystem = false,
        };

        await _repo.AddAsync(status);
        await _repo.SaveChangesAsync(); // cần Id để ghi audit log.

        await _auditLogService.RecordAsync(currentUserId, "Create", "DisposalStatus", status.Id, null, ToSnapshot(status));
        await _repo.SaveChangesAsync();
        return MapToDto(status);
    }

    public async Task<DisposalStatusDto> UpdateAsync(int id, UpsertDisposalStatusRequestDto dto, int currentUserId)
    {
        var status = await _repo.GetByIdAsync(id) ?? throw new DisposalStatusNotFoundException(id);
        var oldValue = ToSnapshot(status);

        var name = dto.Name.Trim();
        await EnsureNameAvailableAsync(name, excludeId: id);

        // Trạng thái hệ thống vẫn sửa được Name/Description/Color/SortOrder; chỉ Code và IsSystem là bất biến.
        status.Name = name;
        status.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        status.Color = dto.Color;
        status.SortOrder = dto.SortOrder;

        await _auditLogService.RecordAsync(currentUserId, "Update", "DisposalStatus", status.Id, oldValue, ToSnapshot(status));
        await _repo.SaveChangesAsync();
        return MapToDto(status);
    }

    public async Task DeleteAsync(int id, int currentUserId)
    {
        var status = await _repo.GetByIdAsync(id) ?? throw new DisposalStatusNotFoundException(id);

        // Xoá được cả bước chính của luồng, nhưng không xoá trạng thái đang có phiếu dùng (làm mất dữ liệu phiếu).
        if (await _repo.IsInUseAsync(id))
        {
            throw new DisposalConflictException(
                $"Trạng thái \"{status.Name}\" đang được dùng bởi phiếu thanh lý. Hãy chuyển/gỡ các phiếu đó trước khi xoá.");
        }

        _repo.Remove(status);
        await _auditLogService.RecordAsync(currentUserId, "Delete", "DisposalStatus", id, ToSnapshot(status), null);
        await _repo.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<DisposalStatusDto>> RestoreDefaultsAsync(int currentUserId)
    {
        var restored = new List<string>();
        foreach (var def in DisposalStatusDefaults.All)
        {
            if (await _repo.GetByCodeAsync(def.Code) is not null)
            {
                continue;
            }

            // Tên đã bị trạng thái khác chiếm thì thêm hậu tố để không vi phạm ràng buộc tên duy nhất.
            var name = await _repo.NameExistsAsync(def.Name) ? $"{def.Name} (mặc định)" : def.Name;
            await _repo.AddAsync(new DisposalStatus
            {
                Code = def.Code,
                Name = name,
                Description = def.Description,
                Color = def.Color,
                SortOrder = def.SortOrder,
                IsSystem = true,
            });
            await _repo.SaveChangesAsync(); // lưu từng bước để tên vừa thêm được tính khi kiểm tra bước kế tiếp.
            restored.Add(def.Code);
        }

        if (restored.Count > 0)
        {
            await _auditLogService.RecordAsync(currentUserId, "RestoreDefaults", "DisposalStatus", 0, null, new { Restored = restored });
            await _repo.SaveChangesAsync();
        }

        return await GetAllAsync();
    }

    private async Task EnsureNameAvailableAsync(string name, int? excludeId)
    {
        if (await _repo.NameExistsAsync(name, excludeId))
        {
            throw new DisposalConflictException($"Tên trạng thái \"{name}\" đã tồn tại.");
        }
    }

    private static object ToSnapshot(DisposalStatus s) => new { s.Code, s.Name, s.Description, s.Color, s.SortOrder, s.IsSystem };

    private static DisposalStatusDto MapToDto(DisposalStatus s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Description = s.Description,
        Color = s.Color,
        SortOrder = s.SortOrder,
        IsSystem = s.IsSystem,
    };
}
