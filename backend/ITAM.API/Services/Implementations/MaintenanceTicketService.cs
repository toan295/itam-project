using ITAM.API.Models.DTOs.Common;
using ITAM.API.Helpers;
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
    private const int NoAccessSentinelDepartmentId = -1; // DepartmentId không tồn tại -> query luôn trả rỗng.
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName };

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
            Priority = ParsePriorityOrDefault(dto.Priority),
            TechnicianId = dto.TechnicianId,
            ReportedDate = DateTime.UtcNow,
        };

        // Có phiếu bảo trì đang chờ xử lý thì tài sản chuyển sang "Bảo trì" (từ Đang dùng hoặc Hỏng). Cùng 1 DbContext
        // với repo phiếu nên 1 lần SaveChanges ghi cả phiếu lẫn trạng thái tài sản (atomic).
        asset.Status = AssetStatus.Maintenance;
        _assetRepo.Update(asset);

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
        // Tài sản còn phiếu chờ xử lý KHÁC thì vẫn giữ "Bảo trì" — chỉ đổi khi đã hết việc bảo trì.
        var asset = ticket.Asset;
        if (asset.Status != AssetStatus.Disposed && !await _repo.HasOtherPendingTicketsAsync(asset.Id, ticket.Id))
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
        int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId,
        string? priority = null, bool? closed = null)
    {
        var parsedStatus = ParseStatusOrThrow(status);
        var parsedPriority = string.IsNullOrWhiteSpace(priority) ? (TicketPriority?)null : ParsePriorityOrThrow(priority);
        var scopedDepartmentId = ResolveDepartmentScope(departmentId, currentUserRole, currentUserDepartmentId);

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, total) = await _repo.GetPagedAsync(
            scopedDepartmentId, assetId, parsedStatus, parsedPriority, closed,
            fromDate?.ToDateTime(TimeOnly.MinValue),
            toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue), // cận trên loại trừ -> gồm trọn ngày toDate.
            technicianScopeUserId: IsTechnician(currentUserRole) ? currentUserId : null,
            page, pageSize);

        return new PagedResultDto<MaintenanceTicketResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<MaintenanceStatsDto> GetStatsAsync(
        int? departmentId, int? assetId, DateOnly? fromDate, DateOnly? toDate,
        string? currentUserRole, int? currentUserDepartmentId)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        var scopedDepartmentId = ResolveDepartmentScope(departmentId, currentUserRole, currentUserDepartmentId);

        var rows = await _repo.GetStatRowsAsync(
            scopedDepartmentId, assetId,
            fromDate?.ToDateTime(TimeOnly.MinValue),
            toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Không có dữ liệu -> đủ khoá với giá trị 0 / mảng rỗng / trung bình null, không phải lỗi (UC-13).
        var countByStatus = Enum.GetValues<TicketStatus>().ToDictionary(s => s.ToString(), _ => 0);
        foreach (var row in rows)
        {
            countByStatus[row.Status.ToString()]++;
        }

        var closedHours = rows
            .Where(r => r.Status != TicketStatus.Pending && r.ResolvedDate.HasValue)
            .Select(r => (r.ResolvedDate!.Value - r.ReportedDate).TotalHours)
            .ToList();

        var byAssetPerYear = rows
            .GroupBy(r => new { r.AssetId, r.AssetCode, r.ReportedDate.Year })
            .Select(g => new AssetMaintenanceYearlyCountDto
            {
                AssetId = g.Key.AssetId,
                AssetCode = g.Key.AssetCode,
                Year = g.Key.Year,
                TicketCount = g.Count(),
            })
            .OrderBy(x => x.AssetCode, StringComparer.Ordinal)
            .ThenBy(x => x.Year)
            .ToList();

        return new MaintenanceStatsDto
        {
            CountByStatus = countByStatus,
            AverageResolutionHours = closedHours.Count > 0 ? closedHours.Average() : null,
            ByAssetPerYear = byAssetPerYear,
        };
    }

    public async Task<MaintenanceTicketResponseDto> UpdatePriorityAsync(
        int id, UpdateTicketPriorityRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var ticket = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);

        if (IsTicketOutOfScope(ticket, currentUserRole, currentUserDepartmentId, currentUserId))
        {
            throw new MaintenanceTicketNotFoundException(id);
        }

        // Phiếu đã đóng không còn việc cần ưu tiên.
        if (ticket.Status != TicketStatus.Pending)
        {
            throw new TicketStatusTransitionNotAllowedException(id, ticket.Status);
        }

        ticket.Priority = ParsePriorityOrThrow(dto.Priority);
        _repo.Update(ticket);
        await _repo.SaveChangesAsync();
        return MapToDto(ticket);
    }

    private static TicketPriority ParsePriorityOrDefault(string? priority) =>
        string.IsNullOrWhiteSpace(priority) ? TicketPriority.Normal : ParsePriorityOrThrow(priority);

    // Enum.TryParse chấp nhận cả chuỗi số ("3") — kiểm tra chặt theo đúng tên enum (cùng lý do với ParseStatusOrThrow).
    private static TicketPriority ParsePriorityOrThrow(string priority)
    {
        if (!Enum.GetNames<TicketPriority>().Contains(priority, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Mức độ khẩn không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<TicketPriority>())}");
        }

        return Enum.Parse<TicketPriority>(priority);
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

    // Phạm vi xem/thao tác trên MỘT phiếu: Manager bị giới hạn theo phòng ban của tài sản. Technician phục vụ
    // mọi phòng ban nhưng chỉ thấy phiếu của mình hoặc phiếu chưa ai nhận — phiếu của kỹ thuật viên khác
    // trả 404 như thể không tồn tại (không lộ thông tin đồng nghiệp).
    private static bool IsTicketOutOfScope(
        MaintenanceTicket ticket, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        if (IsTechnician(currentUserRole))
        {
            return ticket.TechnicianId.HasValue && ticket.TechnicianId != currentUserId;
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
        Priority = t.Priority.ToString(),
        TechnicianId = t.TechnicianId,
        TechnicianName = t.Technician?.FullName,
        ReportedDate = t.ReportedDate,
        ResolvedDate = t.ResolvedDate,
        Notes = t.Notes,
        ResolutionHours = t.ResolvedDate.HasValue ? (t.ResolvedDate.Value - t.ReportedDate).TotalHours : null,
    };
}
