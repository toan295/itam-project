using ITAM.API.Validators;
using ITAM.API.Repositories.Models;
using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITAM.API.Services.Implementations;

public class AssetAllocationService : IAssetAllocationService
{
    private const string AdminRoleName = "Admin IT";
    private const string ManagerRoleName = "Manager";
    private const int NoAccessSentinelDepartmentId = -1;
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName };

    private readonly IAssetAllocationRepository _allocationRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IExclusiveSection _exclusive;
    private readonly ILogger<AssetAllocationService> _logger;

    public AssetAllocationService(
        IAssetAllocationRepository allocationRepository,
        IAssetRepository assetRepository,
        IEmployeeRepository employeeRepository,
        IExclusiveSection exclusive,
        ILogger<AssetAllocationService> logger)
    {
        _allocationRepository = allocationRepository;
        _assetRepository = assetRepository;
        _employeeRepository = employeeRepository;
        _exclusive = exclusive;
        _logger = logger;
    }

    // Khoá dòng tài sản: nếu không, 2 request đồng thời cùng thấy "chưa có phân bổ đang mở" rồi cùng tạo (UC-14).
    public Task<AssetAllocationResponseDto> CreateAsync(
        CreateAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId,
        int? currentUserId = null) =>
        _exclusive.RunAsync(LockTarget.Asset, dto.AssetId, () => CreateCoreAsync(dto, currentUserRole, currentUserDepartmentId, currentUserId));

    private async Task<AssetAllocationResponseDto> CreateCoreAsync(
        CreateAssetAllocationDto dto,
        string? currentUserRole,
        int? currentUserDepartmentId,
        int? currentUserId)
    {
        var asset = await _assetRepository.GetByIdWithDetailsAsync(dto.AssetId)
            ?? throw new AssetNotFoundException(dto.AssetId);

        // Người nhận phải là nhân viên có trong danh mục và đang hoạt động — không nhận tên tự do.
        var employee = await _employeeRepository.GetByIdAsync(dto.EmployeeId)
            ?? throw new ArgumentException($"Nhân viên (EmployeeId={dto.EmployeeId}) không tồn tại.");
        if (!employee.IsActive)
        {
            throw new ArgumentException($"Nhân viên {employee.FullName} đã ngừng hoạt động, không thể nhận tài sản.");
        }

        // Phân quyền phòng ban xử lý thủ công tại Service, cùng pattern với AssetService.
        // Manager chỉ được phân bổ tài sản thuộc đúng phòng ban mình phụ trách và chỉ cho nhân viên của phòng mình.
        if (IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, asset.DepartmentId)
            || IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, employee.DepartmentId))
        {
            throw new DepartmentForbiddenException();
        }

        // Manager chỉ thấy (và chỉ phân bổ cho) nhân viên thường — hồ sơ của Admin IT/Manager/Technician không hiện với họ.
        if (string.Equals(currentUserRole, "Manager", StringComparison.Ordinal)
            && employee.User?.Role?.Name is { } accountRole && !string.Equals(accountRole, "Employee", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Nhân viên (EmployeeId={dto.EmployeeId}) không tồn tại.");
        }

        // Gửi lại cùng một yêu cầu (bấm hai lần, timeout rồi client/proxy retry): nếu lần đầu đã thành công thì trả lại đúng
        // bản ghi đó thay vì báo lỗi "đã được cấp" gây hiểu nhầm. Chỉ coi là gửi lại khi trùng người nhận VÀ ngày giao.
        var open = await _allocationRepository.GetOpenAllocationByAssetIdAsync(asset.Id);
        if (open is not null)
        {
            if (open.EmployeeId == employee.Id && open.AllocatedDate == dto.AllocatedDate)
            {
                var existing = await _allocationRepository.GetByIdWithDetailsAsync(open.Id)
                    ?? throw new AllocationNotFoundException(open.Id);
                return MapToDto(existing);
            }

            throw new AssetAlreadyAllocatedException(asset.Id);
        }

        if (asset.Status != AssetStatus.InUse)
        {
            throw new AssetNotAvailableForAllocationException(asset.Id);
        }

        if (asset.PurchaseDate.HasValue && dto.AllocatedDate < asset.PurchaseDate.Value)
        {
            throw new ArgumentException(
                $"Ngày giao ({dto.AllocatedDate:dd/MM/yyyy}) không được trước ngày mua tài sản ({asset.PurchaseDate.Value:dd/MM/yyyy}).");
        }

        var allocation = new AssetAllocation
        {
            AssetId = asset.Id,
            DepartmentId = employee.DepartmentId, // phòng ban nhận = phòng ban của nhân viên.
            EmployeeId = employee.Id,
            RecipientName = employee.FullName,    // bản chụp họ tên lúc phân bổ.
            RecipientPosition = string.IsNullOrWhiteSpace(employee.Position) ? null : employee.Position.Trim(), // bản chụp chức danh.
            ActiveAssetId = asset.Id,             // chốt chặn database: mỗi tài sản tối đa một phân bổ đang hiệu lực.
            AllocatedDate = dto.AllocatedDate,
            ReturnedDate = null,
            HandoverNote = NormalizeOptional(dto.HandoverNote),
            HandoverReason = NormalizeOptional(dto.HandoverReason),
            HandoverLocation = NormalizeOptional(dto.HandoverLocation),
            HandoverCondition = string.IsNullOrWhiteSpace(dto.HandoverCondition) ? "Tốt" : dto.HandoverCondition.Trim(),
            HandedOverByUserId = currentUserId,
            ReturnCondition = null,
            ReturnNote = null,
            Status = AllocationStatus.Allocated,
        };

        await _allocationRepository.AddAsync(allocation);
        try
        {
            await _allocationRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Chỉ mục duy nhất ActiveAssetId: một phân bổ đang hiệu lực khác vừa được tạo cho tài sản này.
            throw new AssetAlreadyAllocatedException(asset.Id);
        }

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
        int? currentUserDepartmentId,
        int? currentUserId = null)
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
            // Gửi lại đúng yêu cầu thu hồi đã thành công (cùng ngày và tình trạng) -> trả lại kết quả cũ, không báo lỗi.
            if (allocation.ReturnedDate == dto.ReturnedDate
                && string.Equals(allocation.ReturnCondition?.ToString(), dto.Condition, StringComparison.Ordinal))
            {
                var done = await _allocationRepository.GetByIdWithDetailsAsync(id)
                    ?? throw new AllocationNotFoundException(id);
                return MapToDto(done);
            }

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
        allocation.ReceivedByUserId = currentUserId; // bên nhận lại trên biên bản thu hồi.
        allocation.Status = AllocationStatus.Returned;
        allocation.ActiveAssetId = null; // lần phân bổ kết thúc: tài sản được phép phân bổ lại.

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
        int? currentUserDepartmentId,
        string? keyword = null,
        int? employeeId = null,
        int? categoryId = null,
        DateOnly? allocatedFrom = null,
        DateOnly? allocatedTo = null,
        string? handoverCondition = null,
        string? returnCondition = null,
        int? holdingOverDays = null,
        string? sort = null,
        string? departmentScope = null)
    {
        if (departmentScope is not null && departmentScope is not ("handover" or "employee" or "asset"))
        {
            throw new ArgumentException("departmentScope không hợp lệ. Phải là một trong: handover, employee, asset");
        }

        var parsedStatus = ParseStatusOrThrow(status);
        var scopedDepartmentId = ResolveDepartmentScope(
            departmentId,
            currentUserRole,
            currentUserDepartmentId);

        if (allocatedFrom.HasValue && allocatedTo.HasValue && allocatedFrom > allocatedTo)
        {
            throw new ArgumentException("allocatedFrom không được lớn hơn allocatedTo.");
        }

        if (!string.IsNullOrWhiteSpace(handoverCondition) && !CreateAssetAllocationValidator.AllowedConditions.Contains(handoverCondition))
        {
            throw new ArgumentException($"handoverCondition không hợp lệ. Phải là một trong: {string.Join(", ", CreateAssetAllocationValidator.AllowedConditions)}");
        }

        AssetReturnCondition? parsedReturnCondition = null;
        if (!string.IsNullOrWhiteSpace(returnCondition))
        {
            if (!Enum.GetNames<AssetReturnCondition>().Contains(returnCondition, StringComparer.Ordinal))
            {
                throw new ArgumentException($"returnCondition không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<AssetReturnCondition>())}");
            }

            parsedReturnCondition = Enum.Parse<AssetReturnCondition>(returnCondition);
        }

        if (holdingOverDays is < 1 or > 3650)
        {
            throw new ArgumentException("holdingOverDays phải từ 1 đến 3650.");
        }

        if (sort is not null && sort is not ("newest" or "oldest"))
        {
            throw new ArgumentException("sort không hợp lệ. Phải là một trong: newest, oldest");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);

        var extras = new AllocationListExtras
        {
            CategoryId = categoryId,
            AllocatedFrom = allocatedFrom,
            AllocatedTo = allocatedTo,
            HandoverCondition = string.IsNullOrWhiteSpace(handoverCondition) ? null : handoverCondition,
            ReturnCondition = parsedReturnCondition,
            HoldingBefore = holdingOverDays.HasValue ? VietnamTime.Today().AddDays(-holdingOverDays.Value) : null,
            Sort = sort,
            DepartmentScope = departmentScope ?? "handover",
        };

        var (items, totalItems) = await _allocationRepository.GetPagedAsync(
            scopedDepartmentId,
            parsedStatus,
            keyword,
            employeeId,
            page,
            pageSize,
            extras);

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

    public async Task<AllocationDocumentDto> GetDocumentAsync(
        int id,
        string? kind,
        string? currentUserRole,
        int? currentUserDepartmentId)
    {
        var allocation = await _allocationRepository.GetByIdWithDetailsAsync(id)
            ?? throw new AllocationNotFoundException(id);

        if (IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, allocation.DepartmentId))
        {
            throw new AllocationNotFoundException(id);
        }

        var isReturn = string.Equals(kind, "return", StringComparison.OrdinalIgnoreCase);
        if (!isReturn && !string.IsNullOrWhiteSpace(kind) && !string.Equals(kind, "handover", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("kind chỉ nhận \"handover\" hoặc \"return\".");
        }

        if (isReturn && allocation.ReturnedDate is null)
        {
            throw new ArgumentException("Tài sản chưa được thu hồi nên chưa có biên bản thu hồi.");
        }

        var employeeParty = new DocumentPartyDto
        {
            FullName = allocation.Employee?.FullName ?? allocation.RecipientName,
            // Biên bản phản ánh thời điểm giao nhận: chức danh và phòng ban lấy từ bản chụp lúc giao, không đổi theo hồ sơ hiện tại.
            Position = allocation.RecipientPosition ?? allocation.Employee?.Position,
            DepartmentName = allocation.Department?.Name ?? allocation.Employee?.Department?.Name,
        };

        // Bên thực hiện phía công ty (người dùng hệ thống): chức danh = vai trò, bộ phận = phòng ban của người đó.
        static DocumentPartyDto UserParty(User? u) => new()
        {
            FullName = u?.FullName ?? "",
            Position = u?.Role?.Name,
            DepartmentName = u?.Department?.Name,
        };

        var price = await _allocationRepository.GetReferencePriceAsync(allocation.Asset.CategoryId);

        return new AllocationDocumentDto
        {
            Kind = isReturn ? "Return" : "Handover",
            AllocationId = allocation.Id,
            DocumentDate = isReturn ? allocation.ReturnedDate!.Value : allocation.AllocatedDate,
            Location = isReturn ? null : allocation.HandoverLocation,
            Reason = isReturn ? allocation.ReturnNote : allocation.HandoverReason,
            Note = isReturn ? null : allocation.HandoverNote,
            // Bàn giao: công ty (Bên giao) -> nhân viên (Bên nhận). Thu hồi: ngược lại.
            Giver = isReturn ? employeeParty : UserParty(allocation.HandedOverBy),
            Receiver = isReturn ? UserParty(allocation.ReceivedBy) : employeeParty,
            Asset = new DocumentAssetLineDto
            {
                AssetCode = allocation.Asset.AssetCode,
                AssetName = allocation.Asset.Name,
                Unit = "Cái",
                Quantity = 1,
                Condition = isReturn
                    ? (allocation.ReturnCondition == AssetReturnCondition.Damaged ? "Hỏng" : "Tốt")
                    : allocation.HandoverCondition,
                Amount = price,
            },
        };
    }

    // Cảnh báo "quá hạn bảo trì" cho các phân bổ ĐANG MỞ. Quy tắc:
    //  - Mốc tính = lần bảo trì HOÀN TẤT gần nhất của tài sản; chưa có thì ngày mua; chưa có nữa thì ngày giao.
    //    (Phiếu chờ xử lý / không xử lý được không phải "đã bảo trì".)
    //  - Số ngày = hôm nay (giờ Việt Nam) − mốc. Quá hạn khi số ngày > ngưỡng.
    //  - Tài sản đang có phiếu bảo trì chờ xử lý thì không cảnh báo (việc đã được tiếp nhận).
    //  - Tính lại mỗi lần gọi từ dữ liệu hiện tại nên luôn phản ánh phiếu/ngày mới nhất; phân bổ đã thu hồi không còn xuất hiện.
    //  - Mức ưu tiên: High khi số ngày >= 2 x ngưỡng, còn lại Normal.
    public async Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        int thresholdDays,
        string? currentUserRole,
        int? currentUserDepartmentId,
        string? severity = null)
    {
        if (severity is not null && severity is not ("High" or "Normal"))
        {
            throw new ArgumentException("severity không hợp lệ. Phải là một trong: High, Normal");
        }

        thresholdDays = Math.Clamp(thresholdDays, 1, 3650);
        var scopedDepartmentId = ResolveDepartmentScope(
            null,
            currentUserRole,
            currentUserDepartmentId);

        var candidates = await _allocationRepository.GetOpenAllocationCandidatesAsync(scopedDepartmentId);
        var today = VietnamTime.Today();

        var result = new List<OverdueAllocationDto>();
        foreach (var candidate in candidates)
        {
            if (candidate.HasPendingTicket)
            {
                continue;
            }

            var lastMaintenanceDate = candidate.LastMaintenanceDate.HasValue
                ? DateOnly.FromDateTime(candidate.LastMaintenanceDate.Value)
                : (DateOnly?)null;

            DateOnly referenceDate;
            string source;
            if (lastMaintenanceDate.HasValue || candidate.AssetPurchaseDate.HasValue)
            {
                // Lấy mốc muộn hơn giữa "bảo trì gần nhất" và "ngày mua" (bảo trì trước ngày mua là dữ liệu sai, không dùng).
                var maintenance = lastMaintenanceDate ?? DateOnly.MinValue;
                var purchase = candidate.AssetPurchaseDate ?? DateOnly.MinValue;
                if (maintenance >= purchase)
                {
                    referenceDate = maintenance;
                    source = "LastMaintenance";
                }
                else
                {
                    referenceDate = purchase;
                    source = "PurchaseDate";
                }
            }
            else
            {
                referenceDate = candidate.AllocatedDate;
                source = "AllocatedDate";
            }

            var daysSince = today.DayNumber - referenceDate.DayNumber;
            if (daysSince <= thresholdDays)
            {
                continue;
            }

            var level = daysSince >= thresholdDays * 2 ? "High" : "Normal";
            if (severity is not null && level != severity)
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
                DaysSinceLastMaintenance = daysSince,
                ReferenceDate = referenceDate,
                ReferenceSource = source,
                AllocatedDate = candidate.AllocatedDate,
                ThresholdDays = thresholdDays,
                Severity = level,
            });
        }

        return result.OrderByDescending(x => x.DaysSinceLastMaintenance).ThenBy(x => x.AssetCode, StringComparer.Ordinal).ToList();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
        EmployeeId = allocation.EmployeeId,
        EmployeeCode = allocation.Employee?.EmployeeCode,
        EmployeePosition = allocation.RecipientPosition ?? allocation.Employee?.Position,
        AllocatedDate = allocation.AllocatedDate,
        ReturnedDate = allocation.ReturnedDate,
        HandoverNote = allocation.HandoverNote,
        HandoverReason = allocation.HandoverReason,
        HandoverLocation = allocation.HandoverLocation,
        HandoverCondition = allocation.HandoverCondition,
        ReturnCondition = allocation.ReturnCondition?.ToString(),
        ReturnNote = allocation.ReturnNote,
        Status = allocation.Status.ToString(),
        AssetDepartmentName = allocation.Asset?.Department?.Name ?? "",
        EmployeeCurrentDepartmentName = allocation.Employee?.Department?.Name,
        IsOpen = allocation.Status == AllocationStatus.Allocated && allocation.ReturnedDate is null,
        HoldingDays = Math.Max(0, (allocation.ReturnedDate ?? VietnamTime.Today()).DayNumber - allocation.AllocatedDate.DayNumber),
    };
}
