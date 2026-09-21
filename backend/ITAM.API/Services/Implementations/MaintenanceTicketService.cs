using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.MaintenanceTickets;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class MaintenanceTicketService : IMaintenanceTicketService
{
    private const string AdminRoleName = "Admin IT";
    private const string ManagerRoleName = "Manager";
    private const string TechnicianRoleName = "Technician";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int NoAccessSentinelDepartmentId = -1; // DepartmentId không tồn tại -> query luôn trả rỗng.
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName, TechnicianRoleName };

    private readonly IMaintenanceTicketRepository _repo;
    private readonly IAssetRepository _assetRepo;

    public MaintenanceTicketService(IMaintenanceTicketRepository repo, IAssetRepository assetRepo)
    {
        _repo = repo;
        _assetRepo = assetRepo;
    }

    public async Task<MaintenanceTicketResponseDto> CreateAsync(
        CreateMaintenanceTicketRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        // UC-11 E1: AssetId không tồn tại -> 404.
        var asset = await _assetRepo.GetByIdWithDetailsAsync(dto.AssetId)
            ?? throw new AssetNotFoundException(dto.AssetId);

        // Manager/Technician chỉ tạo phiếu cho tài sản phòng ban mình — trả 404 (không lộ tồn tại),
        // đúng pattern AssetService.GetByIdAsync.
        if (IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, asset.DepartmentId))
        {
            throw new AssetNotFoundException(dto.AssetId);
        }

        // UC-11 điều kiện trước: tài sản đang InUse hoặc Broken.
        if (asset.Status is not (AssetStatus.InUse or AssetStatus.Broken))
        {
            throw new AssetNotEligibleForMaintenanceException(asset.Id, asset.Status);
        }

        if (dto.TechnicianId.HasValue)
        {
            // Cùng quy tắc với AssignTechnicianAsync: Technician chỉ được gán cho chính mình,
            // không được lách qua lúc tạo phiếu để gán cho người khác.
            if (IsTechnician(currentUserRole) && dto.TechnicianId != currentUserId)
            {
                throw new TicketAssignmentForbiddenException("Technician chỉ được tự nhận phiếu cho chính mình.");
            }

            await EnsureTechnicianValidAsync(dto.TechnicianId.Value);
        }

        var ticket = new MaintenanceTicket
        {
            AssetId = dto.AssetId,
            IssueDescription = dto.IssueDescription.Trim(),
            Status = TicketStatus.Pending,
            TechnicianId = dto.TechnicianId,
            ReportedDate = DateTime.UtcNow,
        };

        await _repo.AddAsync(ticket);
        await _repo.SaveChangesAsync();

        var created = await _repo.GetByIdWithDetailsAsync(ticket.Id)
            ?? throw new MaintenanceTicketNotFoundException(ticket.Id);
        return MapToDto(created);
    }

    public async Task<MaintenanceTicketResponseDto> AssignTechnicianAsync(
        int id, AssignTechnicianRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var ticket = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);

        // Ngoài phạm vi phòng ban -> 404 như thể không tồn tại.
        if (IsTicketOutOfScope(ticket, currentUserRole, currentUserDepartmentId, currentUserId))
        {
            throw new MaintenanceTicketNotFoundException(id);
        }

        // Phiếu đã đóng là trạng thái cuối, không gán/đổi kỹ thuật viên nữa.
        if (ticket.Status != TicketStatus.Pending)
        {
            throw new TicketStatusTransitionNotAllowedException(id, ticket.Status);
        }

        if (IsTechnician(currentUserRole))
        {
            // UC-12 bước 1 "tự nhận": chỉ được gán cho chính mình, và chỉ khi phiếu chưa ai nhận.
            if (dto.TechnicianId != currentUserId)
            {
                throw new TicketAssignmentForbiddenException("Technician chỉ được tự nhận phiếu cho chính mình.");
            }

            if (ticket.TechnicianId.HasValue)
            {
                throw new TicketAlreadyAssignedException(id);
            }
        }
        else
        {
            // Admin IT / Manager: gán tự do cho bất kỳ Technician hợp lệ nào (kể cả đổi người đã gán).
            await EnsureTechnicianValidAsync(dto.TechnicianId);
        }

        ticket.TechnicianId = dto.TechnicianId;
        _repo.Update(ticket);
        await _repo.SaveChangesAsync();

        // Đọc lại để nạp đúng navigation Technician mới (TechnicianName trong response).
        var updated = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);
        return MapToDto(updated);
    }

    public async Task<MaintenanceTicketResponseDto> UpdateStatusAsync(
        int id, UpdateTicketStatusRequestDto dto, string? currentUserRole, int? currentUserId)
    {
        var ticket = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);

        // UC-12 E1: chỉ Technician được gán cho phiếu này hoặc Admin IT mới được cập nhật.
        // Kiểm tra quyền TRƯỚC trạng thái để người ngoài không dò được trạng thái phiếu của người khác.
        var isAdmin = string.Equals(currentUserRole, AdminRoleName, StringComparison.Ordinal);
        if (!isAdmin && (currentUserId is null || ticket.TechnicianId != currentUserId))
        {
            throw new TicketAssignmentForbiddenException(
                "Chỉ kỹ thuật viên được gán hoặc Admin IT mới được cập nhật phiếu này.");
        }

        // UC-12: chỉ Pending mới được chuyển đi; Resolved/Failed là trạng thái cuối (terminal).
        if (ticket.Status != TicketStatus.Pending)
        {
            throw new TicketStatusTransitionNotAllowedException(id, ticket.Status);
        }

        if (!TryParseClosingStatus(dto.Status, out var newStatus))
        {
            throw new ArgumentException("Status chỉ được chuyển sang Resolved hoặc Failed.");
        }

        ticket.Status = newStatus;
        ticket.ResolvedDate = DateTime.UtcNow;
        ticket.Notes = dto.Notes?.Trim();
        _repo.Update(ticket);

        // UC-12 bước 4: đồng bộ Assets.Status. ticket.Asset và ticket cùng được track bởi 1 AppDbContext,
        // nên 1 lần SaveChangesAsync ghi cả hai trong cùng 1 transaction ngầm của EF Core.
        // Không đụng vào tài sản đã Disposed: phiếu vẫn được đóng, nhưng đóng phiếu không được "hồi sinh" một
        // tài sản đã thanh lý (Admin IT có thể đã thanh lý khi phiếu còn Pending).
        var asset = ticket.Asset;
        if (asset.Status != AssetStatus.Disposed)
        {
            asset.Status = newStatus == TicketStatus.Resolved ? AssetStatus.InUse : AssetStatus.Broken;
            _assetRepo.Update(asset);
        }

        await _repo.SaveChangesAsync();
        return MapToDto(ticket);
    }

    public async Task<MaintenanceTicketResponseDto> GetByIdAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var ticket = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);

        // Cùng lý do với AssetService.GetByIdAsync: 404 thay vì 403 để không lộ tồn tại cho người ngoài phòng ban.
        if (IsTicketOutOfScope(ticket, currentUserRole, currentUserDepartmentId, currentUserId))
        {
            throw new MaintenanceTicketNotFoundException(id);
        }

        return MapToDto(ticket);
    }

    public async Task<PagedResultDto<MaintenanceTicketResponseDto>> GetPagedAsync(
        int? departmentId, int? assetId, string? status, DateOnly? fromDate, DateOnly? toDate,
        int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var parsedStatus = ParseStatusOrThrow(status);
        var scopedDepartmentId = ResolveDepartmentScope(departmentId, currentUserRole, currentUserDepartmentId);

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var (items, total) = await _repo.GetPagedAsync(
            scopedDepartmentId, assetId, parsedStatus,
            fromDate?.ToDateTime(TimeOnly.MinValue),
            toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue), // cận trên loại trừ -> gồm trọn ngày toDate.
            IsTechnician(currentUserRole) ? currentUserId : null, // vẫn thấy phiếu được gán cho mình dù khác phòng ban.
            page, pageSize);

        return new PagedResultDto<MaintenanceTicketResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    private async Task EnsureTechnicianValidAsync(int technicianId)
    {
        if (!await _repo.TechnicianExistsAsync(technicianId))
        {
            throw new ArgumentException($"TechnicianId={technicianId} không tồn tại hoặc không phải Technician.");
        }
    }

    private static TicketStatus? ParseStatusOrThrow(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        // Enum.TryParse chấp nhận cả chuỗi số ("1" -> Resolved) — kiểm tra chặt theo đúng tên enum
        // (cùng lý do với AssetService.TryParseAssetStatus).
        if (!Enum.GetNames<TicketStatus>().Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Status không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<TicketStatus>())}");
        }

        return Enum.Parse<TicketStatus>(status);
    }

    // Chỉ Resolved/Failed là trạng thái hợp lệ để chuyển TỚI (Pending không phải đích của luồng UC-12).
    private static bool TryParseClosingStatus(string value, out TicketStatus status)
    {
        switch (value)
        {
            case "Resolved":
                status = TicketStatus.Resolved;
                return true;
            case "Failed":
                status = TicketStatus.Failed;
                return true;
            default:
                status = default;
                return false;
        }
    }

    // Manager/Technician tự động bị giới hạn theo phòng ban mình — bỏ qua departmentId client gửi lên.
    private static int? ResolveDepartmentScope(
        int? requestedDepartmentId, string? currentUserRole, int? currentUserDepartmentId)
    {
        if (!DepartmentScopedRoles.Contains(currentUserRole))
        {
            return requestedDepartmentId;
        }

        // Claim DepartmentId thiếu/hỏng -> không trả dữ liệu nào (an toàn hơn là mặc định mở toàn bộ).
        return currentUserDepartmentId ?? NoAccessSentinelDepartmentId;
    }

    private static bool IsOutsideDepartmentScope(
        string? currentUserRole, int? currentUserDepartmentId, int targetDepartmentId) =>
        DepartmentScopedRoles.Contains(currentUserRole)
        && currentUserDepartmentId != targetDepartmentId;

    // Phạm vi xem/thao tác trên MỘT phiếu: cùng phòng ban với tài sản, hoặc — riêng Technician — phiếu đang
    // được gán cho chính mình (Admin IT/Manager có thể gán kỹ thuật viên khác phòng ban, người đó vẫn phải
    // thấy và xử lý được phiếu của mình).
    private static bool IsTicketOutOfScope(
        MaintenanceTicket ticket, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        if (IsTechnician(currentUserRole) && currentUserId.HasValue && ticket.TechnicianId == currentUserId)
        {
            return false;
        }

        return IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, ticket.Asset.DepartmentId);
    }

    private static bool IsTechnician(string? currentUserRole) =>
        string.Equals(currentUserRole, TechnicianRoleName, StringComparison.Ordinal);

    private static MaintenanceTicketResponseDto MapToDto(MaintenanceTicket t) => new()
    {
        Id = t.Id,
        AssetId = t.AssetId,
        AssetCode = t.Asset?.AssetCode ?? "",
        AssetName = t.Asset?.Name ?? "",
        IssueDescription = t.IssueDescription,
        Status = t.Status.ToString(),
        TechnicianId = t.TechnicianId,
        TechnicianName = t.Technician?.FullName,
        ReportedDate = t.ReportedDate,
        ResolvedDate = t.ResolvedDate,
        Notes = t.Notes,
        ResolutionHours = t.ResolvedDate.HasValue ? (t.ResolvedDate.Value - t.ReportedDate).TotalHours : null,
    };
}
