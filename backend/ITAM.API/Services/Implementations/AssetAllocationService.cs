using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ITAM.API.Services.Implementations;

public class AssetAllocationService : IAssetAllocationService
{
    private const string AdminRoleName = "Admin IT";
    private const string ManagerRoleName = "Manager";
    private const string TechnicianRoleName = "Technician";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int NoAccessSentinelDepartmentId = -1;
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName, TechnicianRoleName };

    private readonly IAssetAllocationRepository _allocationRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly ILogger<AssetAllocationService> _logger;

    public AssetAllocationService(
        IAssetAllocationRepository allocationRepository,
        IAssetRepository assetRepository,
        ILogger<AssetAllocationService> logger)
    {
        _allocationRepository = allocationRepository;
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<AssetAllocationResponseDto> CreateAsync(
        CreateAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        var asset = await _assetRepository.GetByIdWithDetailsAsync(dto.AssetId)
            ?? throw new AssetNotFoundException(dto.AssetId);

        // Phân quyền phòng ban xử lý thủ công tại Service, cùng pattern với AssetService.
        // Manager chỉ được phân bổ tài sản thuộc đúng phòng ban mình phụ trách và không được
        // chọn một phòng ban nhận khác phòng ban của mình.
        if (IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, asset.DepartmentId)
            || IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, dto.DepartmentId))
        {
            throw new DepartmentForbiddenException();
        }

        if (asset.Status != AssetStatus.InUse)
        {
            throw new AssetNotAvailableForAllocationException(asset.Id);
        }

        if (!await _assetRepository.DepartmentExistsAsync(dto.DepartmentId))
        {
            throw new ArgumentException($"Phòng ban (DepartmentId={dto.DepartmentId}) không tồn tại.");
        }

        if (await _allocationRepository.HasOpenAllocationAsync(asset.Id))
        {
            throw new AssetAlreadyAllocatedException(asset.Id);
        }

        var allocation = new AssetAllocation
        {
            AssetId = asset.Id,
            DepartmentId = dto.DepartmentId,
            RecipientName = dto.RecipientName.Trim(),
            AllocatedDate = dto.AllocatedDate,
            ReturnedDate = null,
            HandoverNote = string.IsNullOrWhiteSpace(dto.HandoverNote) ? null : dto.HandoverNote.Trim(),
            ReturnCondition = null,
            ReturnNote = null,
            Status = AllocationStatus.Allocated,
        };

        await _allocationRepository.AddAsync(allocation);
        await _allocationRepository.SaveChangesAsync();

        _logger.LogInformation(
            "Đã phân bổ AssetId={AssetId} cho DepartmentId={DepartmentId}, người nhận {RecipientName}.",
            allocation.AssetId,
            allocation.DepartmentId,
            allocation.RecipientName);

        var created = await _allocationRepository.GetByIdWithDetailsAsync(allocation.Id)
            ?? throw new AllocationNotFoundException(allocation.Id);

        return MapToDto(created);
    }

    public async Task<AssetAllocationResponseDto> ReturnAsync(
        int id,
        ReturnAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        // Tracked + Include(Asset): allocation và asset của nó cùng nằm trong 1 AppDbContext, cho phép
        // sửa cả 2 rồi ghi bằng đúng 1 SaveChangesAsync — đúng quy tắc UC-15 "phải nằm trong cùng 1
        // transaction" (không tách thành 2 lần SaveChangesAsync riêng biệt, vì lần thứ 2 có thể lỗi
        // giữa chừng và để lại allocation đã "Returned" nhưng Asset chưa chuyển Broken).
        var allocation = await _allocationRepository.GetEntityByIdAsync(id)
            ?? throw new AllocationNotFoundException(id);

        if (IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, allocation.DepartmentId))
        {
            throw new DepartmentForbiddenException();
        }

        if (allocation.Status != AllocationStatus.Allocated || allocation.ReturnedDate is not null)
        {
            throw new AllocationAlreadyReturnedException(id);
        }

        if (dto.ReturnedDate < allocation.AllocatedDate)
        {
            throw new ArgumentException("Ngày thu hồi không được trước ngày phân bổ.");
        }

        // dto.Condition đã được validator chặn chỉ còn "Good"/"Damaged" — Enum.Parse ở đây không thể ném lỗi.
        var condition = Enum.Parse<AssetReturnCondition>(dto.Condition);

        allocation.ReturnedDate = dto.ReturnedDate;
        allocation.ReturnCondition = condition;
        allocation.ReturnNote = string.IsNullOrWhiteSpace(dto.ReturnNote) ? null : dto.ReturnNote.Trim();
        allocation.Status = AllocationStatus.Returned;

        // UC-15 bước 4: tài sản nhận lại ở tình trạng Damaged phải chuyển sang Broken.
        // Good giữ nguyên trạng thái hiện tại của Asset. allocation.Asset đã được Include ở trên nên
        // không cần truy vấn lại — Update() chỉ đánh dấu Modified trên entity đã tracked sẵn.
        if (condition == AssetReturnCondition.Damaged)
        {
            allocation.Asset.Status = AssetStatus.Broken;
            _assetRepository.Update(allocation.Asset);
        }

        // Một lần SaveChangesAsync duy nhất cho cả allocation lẫn asset (nếu có) — cả 2 repository dùng
        // chung 1 AppDbContext (Scoped) nên đây là 1 transaction ngầm duy nhất của EF Core.
        await _allocationRepository.SaveChangesAsync();

        _logger.LogInformation(
            "Đã thu hồi AllocationId={AllocationId}, AssetId={AssetId}, Condition={Condition}.",
            allocation.Id,
            allocation.AssetId,
            condition);

        var returned = await _allocationRepository.GetByIdWithDetailsAsync(id)
            ?? throw new AllocationNotFoundException(id);
        return MapToDto(returned);
    }

    public async Task<PagedResultDto<AssetAllocationResponseDto>> GetPagedAsync(
        int? departmentId,
        string? status,
        int page,
        int pageSize,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        var parsedStatus = ParseStatusOrThrow(status);
        var scopedDepartmentId = ResolveDepartmentScope(
            departmentId,
            currentUserRole,
            currentUserDepartmentId);

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var (items, totalItems) = await _allocationRepository.GetPagedAsync(
            scopedDepartmentId,
            parsedStatus,
            page,
            pageSize);

        return new PagedResultDto<AssetAllocationResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<AssetAllocationResponseDto> GetByIdAsync(
        int id,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(id)
            ?? throw new AllocationNotFoundException(id);

        // Giống AssetService: GET chi tiết ngoài phạm vi phòng ban trả 404 để không lộ dữ liệu.
        if (IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, allocation.DepartmentId))
        {
            throw new AllocationNotFoundException(id);
        }

        return MapToDto(allocation);
    }

    public async Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        int thresholdDays,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        thresholdDays = Math.Clamp(thresholdDays, 1, 3650);
        var scopedDepartmentId = ResolveDepartmentScope(
            null,
            currentUserRole,
            currentUserDepartmentId);

        var candidates = await _allocationRepository.GetOpenAllocationCandidatesAsync(scopedDepartmentId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = new List<OverdueAllocationDto>();
        foreach (var candidate in candidates)
        {
            var lastMaintenanceDate = candidate.LastMaintenanceDate.HasValue
                ? DateOnly.FromDateTime(candidate.LastMaintenanceDate.Value)
                : (DateOnly?)null;

            // Chưa từng có phiếu bảo trì -> dùng PurchaseDate; nếu cũng không có -> dùng AllocatedDate
            // (Mục B2, "cảnh báo quá hạn bảo trì": đề xuất cụ thể hoá, chưa có UC riêng mô tả chi tiết).
            var comparisonDate = lastMaintenanceDate ?? candidate.AssetPurchaseDate ?? candidate.AllocatedDate;
            var daysSinceLastMaintenance = today.DayNumber - comparisonDate.DayNumber;
            if (daysSinceLastMaintenance <= thresholdDays)
            {
                continue;
            }

            result.Add(new OverdueAllocationDto
            {
                AllocationId = candidate.AllocationId,
                AssetId = candidate.AssetId,
                AssetCode = candidate.AssetCode,
                AssetName = candidate.AssetName,
                DepartmentName = candidate.DepartmentName,
                RecipientName = candidate.RecipientName,
                LastMaintenanceDate = lastMaintenanceDate,
                DaysSinceLastMaintenance = daysSinceLastMaintenance,
            });
        }

        return result.OrderByDescending(x => x.DaysSinceLastMaintenance).ToList();
    }

    private static AllocationStatus? ParseStatusOrThrow(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.GetNames<AllocationStatus>().Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Status không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<AllocationStatus>())}");
        }

        return Enum.Parse<AllocationStatus>(status);
    }

    private static int? ResolveDepartmentScope(
        int? requestedDepartmentId,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        if (!DepartmentScopedRoles.Contains(currentUserRole))
        {
            return requestedDepartmentId;
        }

        return currentUserDepartmentId ?? NoAccessSentinelDepartmentId;
    }

    private static bool IsManagerOutsideDepartment(
        string? currentUserRole,
        int? currentUserDepartmentId,
        int targetDepartmentId) =>
        string.Equals(currentUserRole, ManagerRoleName, StringComparison.Ordinal)
        && currentUserDepartmentId != targetDepartmentId;

    private static bool IsOutsideDepartmentScope(
        string? currentUserRole,
        int? currentUserDepartmentId,
        int targetDepartmentId) =>
        DepartmentScopedRoles.Contains(currentUserRole)
        && currentUserDepartmentId != targetDepartmentId;

    private static AssetAllocationResponseDto MapToDto(AssetAllocation allocation) => new()
    {
        Id = allocation.Id,
        AssetId = allocation.AssetId,
        AssetCode = allocation.Asset?.AssetCode ?? "",
        AssetName = allocation.Asset?.Name ?? "",
        DepartmentId = allocation.DepartmentId,
        DepartmentName = allocation.Department?.Name ?? "",
        RecipientName = allocation.RecipientName,
        AllocatedDate = allocation.AllocatedDate,
        ReturnedDate = allocation.ReturnedDate,
        HandoverNote = allocation.HandoverNote,
        ReturnCondition = allocation.ReturnCondition?.ToString(),
        ReturnNote = allocation.ReturnNote,
        Status = allocation.Status.ToString(),
    };
}
