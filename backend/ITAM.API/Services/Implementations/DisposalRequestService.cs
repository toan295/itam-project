using ITAM.API.Models.DTOs.Common;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class DisposalRequestService : IDisposalRequestService
{
    private const string ManagerRoleName = "Manager";
    private const int NoAccessSentinelDepartmentId = -1; // DepartmentId không tồn tại -> query luôn rỗng.

    private readonly IDisposalRequestRepository _repo;
    private readonly IDisposalStatusRepository _statusRepo;
    private readonly IAssetRepository _assetRepo;
    private readonly IAssetAllocationRepository _allocationRepo;
    private readonly IMaintenanceTicketRepository _ticketRepo;
    private readonly IAuditLogService _auditLogService;
    private readonly IExclusiveSection _exclusive;

    public DisposalRequestService(
        IDisposalRequestRepository repo,
        IDisposalStatusRepository statusRepo,
        IAssetRepository assetRepo,
        IAssetAllocationRepository allocationRepo,
        IMaintenanceTicketRepository ticketRepo,
        IAuditLogService auditLogService,
        IExclusiveSection exclusive)
    {
        _repo = repo;
        _statusRepo = statusRepo;
        _assetRepo = assetRepo;
        _allocationRepo = allocationRepo;
        _ticketRepo = ticketRepo;
        _auditLogService = auditLogService;
        _exclusive = exclusive;
    }

    public async Task<PagedResultDto<DisposalRequestResponseDto>> GetPagedAsync(
        int? departmentId, int? assetId, string? statusCode, int page, int pageSize,
        string? currentUserRole, int? currentUserDepartmentId, int currentUserId)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        // Manager chỉ thấy phiếu của tài sản thuộc phòng ban mình; Admin IT/Technician thấy tất cả.
        var scopedDepartmentId = IsManager(currentUserRole)
            ? currentUserDepartmentId ?? NoAccessSentinelDepartmentId
            : departmentId;

        var (items, total) = await _repo.GetPagedAsync(
            scopedDepartmentId, assetId, statusCode, IsTechnician(currentUserRole) ? currentUserId : null, page, pageSize);
        return new PagedResultDto<DisposalRequestResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<DisposalRequestResponseDto> GetByIdAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int currentUserId)
    {
        var request = await GetScopedAsync(id, currentUserRole, currentUserDepartmentId);
        // Technician chỉ xem được phiếu của chính mình — phiếu của kỹ thuật viên khác trả 404.
        if (IsTechnician(currentUserRole)
            && request.InspectedByUserId != currentUserId && request.ProposedByUserId != currentUserId)
        {
            throw new DisposalRequestNotFoundException(id);
        }

        return MapToDto(request);
    }

    // Khoá dòng tài sản: nếu không, 2 request đồng thời cùng thấy "chưa có phiếu đang mở" rồi cùng tạo phiếu.
    public async Task<PagedResultDto<DisposalCandidateDto>> GetCandidatesAsync(
        int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var scoped = IsManager(currentUserRole) ? currentUserDepartmentId ?? NoAccessSentinelDepartmentId : (int?)null;

        var (items, total) = await _repo.GetDisposalCandidatesAsync(scoped, page, pageSize);
        return new PagedResultDto<DisposalCandidateDto>
        {
            Items = items.Select(a => new DisposalCandidateDto
            {
                AssetId = a.Id,
                AssetCode = a.AssetCode,
                AssetName = a.Name,
                CategoryName = a.Category?.Name ?? "",
                DepartmentId = a.DepartmentId,
                DepartmentName = a.Department?.Name ?? "",
                SerialNumber = a.SerialNumber,
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public Task<DisposalRequestResponseDto> CreateAsync(CreateDisposalRequestDto dto, int currentUserId) =>
        _exclusive.RunAsync(LockTarget.Asset, dto.AssetId, () => CreateCoreAsync(dto, currentUserId));

    private async Task<DisposalRequestResponseDto> CreateCoreAsync(CreateDisposalRequestDto dto, int currentUserId)
    {
        var asset = await _assetRepo.GetByIdWithDetailsAsync(dto.AssetId)
            ?? throw new AssetNotFoundException(dto.AssetId);

        if (asset.Status == AssetStatus.Disposed)
        {
            throw new DisposalConflictException($"Tài sản {asset.AssetCode} đã được thanh lý.");
        }

        if (await _repo.HasOpenRequestForAssetAsync(asset.Id))
        {
            throw new DisposalConflictException(
                $"Tài sản {asset.AssetCode} đang có một phiếu thanh lý chưa kết thúc.");
        }

        var inspected = await GetStatusAsync(DisposalStatusCodes.Inspected);
        var request = new DisposalRequest
        {
            AssetId = asset.Id,
            StatusId = inspected.Id,
            InspectionNote = dto.InspectionNote.Trim(),
            InspectedByUserId = currentUserId,
            InspectedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(request);
        await _repo.SaveChangesAsync(); // cần Id để ghi audit log.

        await _auditLogService.RecordAsync(
            currentUserId, "Create", "DisposalRequest", request.Id, null,
            new { Status = DisposalStatusCodes.Inspected, AssetId = asset.Id });
        await _repo.SaveChangesAsync();

        return await ReloadAsync(request.Id);
    }

    public async Task<DisposalRequestResponseDto> ProposeAsync(int id, ProposeDisposalRequestDto dto, int currentUserId)
    {
        var request = await GetOrThrowAsync(id);

        // Chỉ kỹ thuật viên đã lập biên bản kiểm tra mới được đề xuất — cùng quy tắc "chỉ xem được phiếu của
        // chính mình" ở GetByIdAsync; phiếu của người khác trả 404 như thể không tồn tại (không lộ thông tin).
        if (request.InspectedByUserId != currentUserId)
        {
            throw new DisposalRequestNotFoundException(id);
        }

        EnsureStatus(request, DisposalStatusCodes.Inspected, "đề xuất");

        request.Status = await GetStatusAsync(DisposalStatusCodes.Proposed);
        request.Reason = dto.Reason.Trim();
        request.DisposalMethod = string.IsNullOrWhiteSpace(dto.DisposalMethod) ? null : dto.DisposalMethod.Trim();
        request.ProposedByUserId = currentUserId;
        request.ProposedAt = DateTime.UtcNow;

        return await SaveTransitionAsync(request, currentUserId, "Propose", DisposalStatusCodes.Inspected);
    }

    public Task<DisposalRequestResponseDto> ApproveAsync(
        int id, ReviewDisposalRequestDto dto, int currentUserId, int? currentUserDepartmentId) =>
        ReviewAsync(id, dto, currentUserId, currentUserDepartmentId, approve: true);

    public Task<DisposalRequestResponseDto> RejectAsync(
        int id, ReviewDisposalRequestDto dto, int currentUserId, int? currentUserDepartmentId) =>
        ReviewAsync(id, dto, currentUserId, currentUserDepartmentId, approve: false);

    private async Task<DisposalRequestResponseDto> ReviewAsync(
        int id, ReviewDisposalRequestDto dto, int currentUserId, int? currentUserDepartmentId, bool approve)
    {
        // Manager chỉ duyệt phiếu thuộc phòng ban mình — ngoài phạm vi trả 404 như thể không tồn tại.
        var request = await GetScopedAsync(id, ManagerRoleName, currentUserDepartmentId);
        EnsureStatus(request, DisposalStatusCodes.Proposed, approve ? "duyệt" : "từ chối");

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (!approve && note is null)
        {
            throw new ArgumentException("Phải nhập lý do khi từ chối đề xuất thanh lý.");
        }

        var target = approve ? DisposalStatusCodes.Approved : DisposalStatusCodes.Rejected;
        request.Status = await GetStatusAsync(target);
        request.ReviewNote = note;
        request.ReviewedByUserId = currentUserId;
        request.ReviewedAt = DateTime.UtcNow;

        return await SaveTransitionAsync(request, currentUserId, approve ? "Approve" : "Reject", DisposalStatusCodes.Proposed);
    }

    // Khoá cùng dòng tài sản với phân bổ: việc kiểm tra "còn phân bổ đang mở" và việc đặt Disposed phải liền mạch,
    // nếu không một phân bổ mới có thể chen vào giữa và để lại tài sản đã thanh lý nhưng đang được phân bổ.
    public async Task<DisposalRequestResponseDto> CompleteAsync(int id, CompleteDisposalRequestDto dto, int currentUserId)
    {
        var assetId = (await GetOrThrowAsync(id)).AssetId;
        return await _exclusive.RunAsync(LockTarget.Asset, assetId, () => CompleteCoreAsync(id, dto, currentUserId));
    }

    private async Task<DisposalRequestResponseDto> CompleteCoreAsync(int id, CompleteDisposalRequestDto dto, int currentUserId)
    {
        var request = await GetOrThrowAsync(id);
        EnsureStatus(request, DisposalStatusCodes.Approved, "thực hiện thanh lý");

        // Cùng quy tắc UC-07 E1: tài sản còn phân bổ chưa thu hồi thì phải thu hồi (UC-15) trước.
        if (await _allocationRepo.HasOpenAllocationAsync(request.AssetId))
        {
            throw new AssetHasOpenAllocationException(request.AssetId);
        }

        if (request.Asset.Status == AssetStatus.Disposed)
        {
            throw new DisposalConflictException($"Tài sản {request.Asset.AssetCode} đã được thanh lý trước đó.");
        }

        // Asset và phiếu cùng được track bởi 1 DbContext -> 1 lần SaveChanges ghi cả hai (atomic).
        request.Asset.Status = AssetStatus.Disposed;

        // Tài sản đã thanh lý thì các phiếu bảo trì đang chờ không còn ý nghĩa: đóng tự động (Không xử lý được).
        foreach (var ticket in await _ticketRepo.GetPendingByAssetAsync(request.AssetId))
        {
            ticket.Status = TicketStatus.Failed;
            ticket.ResolvedDate = DateTime.UtcNow;
            ticket.Notes = "Đóng tự động: tài sản đã được thanh lý.";
        }

        request.Status = await GetStatusAsync(DisposalStatusCodes.Completed);
        request.CompletionNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        request.CompletedByUserId = currentUserId;
        request.CompletedAt = DateTime.UtcNow;

        return await SaveTransitionAsync(request, currentUserId, "Complete", DisposalStatusCodes.Approved);
    }

    public async Task<DisposalRequestResponseDto> SetSubStatusAsync(int id, int? subStatusId, int currentUserId)
    {
        var request = await GetOrThrowAsync(id);
        if (DisposalStatusCodes.FinalCodes.Contains(request.Status.Code))
        {
            throw new DisposalConflictException(
                $"Phiếu đã kết thúc ở trạng thái \"{request.Status.Name}\", không thể gán trạng thái phụ.");
        }

        DisposalStatus? sub = null;
        if (subStatusId.HasValue)
        {
            sub = await _statusRepo.GetByIdAsync(subStatusId.Value)
                ?? throw new DisposalStatusNotFoundException(subStatusId.Value);
            if (sub.IsSystem)
            {
                throw new ArgumentException("Chỉ được gán trạng thái phụ (do Admin thêm); bước chính của luồng đổi qua các thao tác duyệt/thanh lý.");
            }
        }

        var oldSub = request.SubStatus?.Name;
        request.SubStatus = sub;
        request.SubStatusId = sub?.Id;

        await _auditLogService.RecordAsync(
            currentUserId, "SetSubStatus", "DisposalRequest", request.Id,
            new { SubStatus = oldSub }, new { SubStatus = sub?.Name });
        await _repo.SaveChangesAsync();
        return MapToDto(request);
    }

    // ----- helpers -----

    private async Task<DisposalRequestResponseDto> SaveTransitionAsync(
        DisposalRequest request, int currentUserId, string action, string fromCode)
    {
        await _auditLogService.RecordAsync(
            currentUserId, action, "DisposalRequest", request.Id,
            new { Status = fromCode }, new { Status = request.Status.Code });
        await _repo.SaveChangesAsync();
        return MapToDto(request);
    }

    private async Task<DisposalRequest> GetOrThrowAsync(int id) =>
        await _repo.GetByIdWithDetailsAsync(id) ?? throw new DisposalRequestNotFoundException(id);

    private async Task<DisposalRequest> GetScopedAsync(int id, string? currentUserRole, int? currentUserDepartmentId)
    {
        var request = await GetOrThrowAsync(id);
        if (IsManager(currentUserRole) && request.Asset.DepartmentId != currentUserDepartmentId)
        {
            throw new DisposalRequestNotFoundException(id);
        }

        return request;
    }

    // Bước chính có thể đã bị Admin IT xoá: báo lỗi rõ (409) kèm cách khắc phục thay vì lỗi 500.
    private async Task<DisposalStatus> GetStatusAsync(string code) =>
        await _statusRepo.GetByCodeAsync(code)
        ?? throw new DisposalConflictException(
            $"Bước \"{code}\" của quy trình thanh lý đã bị xoá. Admin IT cần bấm \"Khôi phục bước mặc định\" ở tab Quản lý trạng thái.");

    private async Task<DisposalRequestResponseDto> ReloadAsync(int id) => MapToDto(await GetOrThrowAsync(id));

    private static void EnsureStatus(DisposalRequest request, string expectedCode, string actionName)
    {
        if (request.Status.Code != expectedCode)
        {
            throw new DisposalConflictException(
                $"Không thể {actionName} phiếu đang ở trạng thái \"{request.Status.Name}\".");
        }
    }

    private static bool IsTechnician(string? role) => string.Equals(role, "Technician", StringComparison.Ordinal);

    private static bool IsManager(string? role) => string.Equals(role, ManagerRoleName, StringComparison.Ordinal);

    private static DisposalRequestResponseDto MapToDto(DisposalRequest r) => new()
    {
        Id = r.Id,
        AssetId = r.AssetId,
        AssetCode = r.Asset.AssetCode,
        AssetName = r.Asset.Name,
        AssetStatus = r.Asset.Status.ToString(),
        DepartmentId = r.Asset.DepartmentId,
        DepartmentName = r.Asset.Department?.Name ?? "",
        StatusId = r.StatusId,
        StatusCode = r.Status.Code,
        StatusName = r.Status.Name,
        StatusColor = r.Status.Color,
        SubStatusId = r.SubStatusId,
        SubStatusName = r.SubStatus?.Name,
        SubStatusColor = r.SubStatus?.Color,
        InspectionNote = r.InspectionNote,
        InspectedByName = r.InspectedBy?.FullName ?? "",
        InspectedAt = r.InspectedAt,
        Reason = r.Reason,
        DisposalMethod = r.DisposalMethod,
        ProposedByName = r.ProposedBy?.FullName,
        ProposedAt = r.ProposedAt,
        ReviewNote = r.ReviewNote,
        ReviewedByName = r.ReviewedBy?.FullName,
        ReviewedAt = r.ReviewedAt,
        CompletionNote = r.CompletionNote,
        CompletedByName = r.CompletedBy?.FullName,
        CompletedAt = r.CompletedAt,
        CreatedAt = r.CreatedAt,
    };
}
