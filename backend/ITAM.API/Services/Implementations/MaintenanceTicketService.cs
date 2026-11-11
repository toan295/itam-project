using ITAM.API.Repositories.Models;
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
    private const string EmployeeRoleName = "Employee";
    private const int MinResultLength = 5; // độ dài tối thiểu của kết quả xử lý/lý do khi đóng phiếu.
    private const int NoAccessSentinelDepartmentId = -1; // DepartmentId không tồn tại -> query luôn trả rỗng.
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName };

    private readonly IMaintenanceTicketRepository _repo;
    private readonly IAssetRepository _assetRepo;
    private readonly IEmployeeRepository _employeeRepo;

    public MaintenanceTicketService(
        IMaintenanceTicketRepository repo, IAssetRepository assetRepo, IEmployeeRepository employeeRepo)
    {
        _repo = repo;
        _assetRepo = assetRepo;
        _employeeRepo = employeeRepo;
    }

    public async Task<MaintenanceTicketResponseDto> CreateAsync(
        CreateMaintenanceTicketRequestDto dto, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        // Kỹ thuật viên không tạo phiếu: phiếu do người dùng được cấp tài sản báo lỗi, Admin IT phân công, KTV đi kiểm tra.
        if (IsTechnician(currentUserRole))
        {
            throw new TicketAssignmentForbiddenException(
                "Kỹ thuật viên không tạo phiếu bảo trì. Phiếu do người dùng được cấp tài sản báo lỗi và Admin IT phân công xử lý.");
        }

        // UC-11 E1: AssetId không tồn tại -> 404.
        var asset = await _assetRepo.GetByIdWithDetailsAsync(dto.AssetId)
            ?? throw new AssetNotFoundException(dto.AssetId);

        // Nhân viên (vai trò Employee) chỉ được báo lỗi tài sản MÌNH ĐANG ĐƯỢC CẤP (phân bổ đang mở) — trả 404 như
        // thể không tồn tại để không lộ tài sản của người khác.
        var isEmployee = IsEmployee(currentUserRole);
        if (isEmployee)
        {
            var employee = currentUserId.HasValue ? await _employeeRepo.GetByUserIdAsync(currentUserId.Value) : null;
            if (employee is null || !employee.IsActive
                || !await _employeeRepo.HasOpenAllocationForAssetAsync(employee.Id, dto.AssetId))
            {
                throw new AssetNotFoundException(dto.AssetId);
            }
        }

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

        // Nhân viên không tự chọn người xử lý — Admin IT/Manager/Technician sẽ nhận và xử lý phiếu.
        var technicianId = isEmployee ? null : dto.TechnicianId;
        if (technicianId.HasValue)
        {
            await EnsureTechnicianValidAsync(technicianId.Value);
        }

        var actorName = await GetActorNameAsync(currentUserId);
        var ticket = new MaintenanceTicket
        {
            AssetId = dto.AssetId,
            IssueDescription = dto.IssueDescription.Trim(),
            Status = TicketStatus.Pending,
            Priority = ParsePriorityOrDefault(dto.Priority),
            TechnicianId = technicianId,
            ReportedByUserId = currentUserId,
            ReportedDate = DateTime.UtcNow,
        };
        ticket.History.Add(NewHistory("Created", null, ticket.Priority.ToString(), dto.IssueDescription.Trim(), currentUserId, actorName));
        if (technicianId.HasValue)
        {
            ticket.History.Add(NewHistory("Assigned", null, await _repo.GetUserFullNameAsync(technicianId.Value), null, currentUserId, actorName));
        }

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

        // Phân công là việc của Admin IT / Manager; kỹ thuật viên không tự nhận phiếu.
        if (IsTechnician(currentUserRole) || IsEmployee(currentUserRole))
        {
            throw new TicketAssignmentForbiddenException("Chỉ Admin IT hoặc Manager mới phân công kỹ thuật viên cho phiếu.");
        }

        ConcurrencyConflictException.EnsureCurrent(ticket.Version, dto.Version);

        // Gán cho Technician hợp lệ (đang hoạt động); đổi người đã gán thì phải nêu lý do để truy vết điều phối.
        await EnsureTechnicianValidAsync(dto.TechnicianId);
        if (ticket.TechnicianId == dto.TechnicianId)
        {
            throw new ArgumentException("Phiếu đã được phân công cho kỹ thuật viên này.");
        }

        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        var isReassign = ticket.TechnicianId.HasValue;
        if (isReassign && reason is null)
        {
            throw new ArgumentException("Vui lòng nhập lý do khi đổi kỹ thuật viên phụ trách.");
        }

        var newName = await _repo.GetUserFullNameAsync(dto.TechnicianId);
        ticket.History.Add(NewHistory(isReassign ? "Reassigned" : "Assigned",
            isReassign ? ticket.Technician?.FullName : null, newName, reason, currentUserId, await GetActorNameAsync(currentUserId)));
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

        ConcurrencyConflictException.EnsureCurrent(ticket.Version, dto.Version);

        if (!TryParseClosingStatus(dto.Status, out var newStatus))
        {
            throw new ArgumentException("Status chỉ được chuyển sang Resolved hoặc Failed.");
        }

        // Đóng phiếu phải có kết quả: việc đã làm (Resolved) hoặc lý do không xử lý được (Failed).
        var notes = dto.Notes?.Trim();
        if (string.IsNullOrEmpty(notes) || notes.Length < MinResultLength)
        {
            throw new ArgumentException(newStatus == TicketStatus.Resolved
                ? $"Vui lòng mô tả kết quả xử lý (ít nhất {MinResultLength} ký tự)."
                : $"Vui lòng nêu lý do không xử lý được (ít nhất {MinResultLength} ký tự).");
        }

        ticket.History.Add(NewHistory(newStatus.ToString(), ticket.Status.ToString(), newStatus.ToString(), notes,
            currentUserId, await GetActorNameAsync(currentUserId)));
        ticket.Status = newStatus;
        ticket.ResolvedDate = DateTime.UtcNow;
        ticket.Notes = notes;
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
        string? priority = null, bool? closed = null, string? keyword = null,
        int? technicianId = null, bool? unassigned = null, int? categoryId = null, int? pendingOverDays = null, string? sort = null)
    {
        var parsedStatus = ParseStatusOrThrow(status);
        var parsedPriority = string.IsNullOrWhiteSpace(priority) ? (TicketPriority?)null : ParsePriorityOrThrow(priority);
        var scopedDepartmentId = ResolveDepartmentScope(departmentId, currentUserRole, currentUserDepartmentId);

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ArgumentException("fromDate không được lớn hơn toDate.");
        }

        if (unassigned == true && technicianId.HasValue)
        {
            throw new ArgumentException("Không thể vừa lọc theo kỹ thuật viên vừa lọc phiếu chưa phân công.");
        }

        if (pendingOverDays is < 1 or > 3650)
        {
            throw new ArgumentException("pendingOverDays phải từ 1 đến 3650.");
        }

        if (sort is not null && sort is not ("urgency" or "newest" or "oldest"))
        {
            throw new ArgumentException("sort không hợp lệ. Phải là một trong: urgency, newest, oldest");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);

        var extras = new TicketListExtras
        {
            TechnicianId = technicianId,
            Unassigned = unassigned,
            CategoryId = categoryId,
            PendingBefore = pendingOverDays.HasValue ? DateTime.UtcNow.AddDays(-pendingOverDays.Value) : null,
            Sort = sort,
        };

        var (items, total) = await _repo.GetPagedAsync(
            scopedDepartmentId, assetId, parsedStatus, parsedPriority, closed,
            fromDate?.ToDateTime(TimeOnly.MinValue),
            toDate?.AddDays(1).ToDateTime(TimeOnly.MinValue), // cận trên loại trừ -> gồm trọn ngày toDate.
            technicianScopeUserId: IsTechnician(currentUserRole) ? currentUserId : null,
            page, pageSize, keyword, extras);

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

        ConcurrencyConflictException.EnsureCurrent(ticket.Version, dto.Version);

        var newPriority = ParsePriorityOrThrow(dto.Priority);
        if (newPriority != ticket.Priority)
        {
            ticket.History.Add(NewHistory("PriorityChanged", ticket.Priority.ToString(), newPriority.ToString(), null,
                currentUserId, await GetActorNameAsync(currentUserId)));
        }

        ticket.Priority = newPriority;
        _repo.Update(ticket);
        await _repo.SaveChangesAsync();
        return MapToDto(ticket);
    }

    public async Task<List<MaintenanceTicketHistoryDto>> GetHistoryAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var ticket = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new MaintenanceTicketNotFoundException(id);

        if (IsTicketOutOfScope(ticket, currentUserRole, currentUserDepartmentId, currentUserId))
        {
            throw new MaintenanceTicketNotFoundException(id);
        }

        var history = await _repo.GetHistoryAsync(id);
        return history.Select(h => new MaintenanceTicketHistoryDto
        {
            Id = h.Id,
            Action = h.Action,
            FromValue = h.FromValue,
            ToValue = h.ToValue,
            Reason = h.Reason,
            ActorUserId = h.ActorUserId,
            ActorName = h.ActorName,
            CreatedAt = h.CreatedAt,
        }).ToList();
    }

    private async Task<string?> GetActorNameAsync(int? userId) =>
        userId.HasValue ? await _repo.GetUserFullNameAsync(userId.Value) : null;

    private static MaintenanceTicketHistory NewHistory(
        string action, string? from, string? to, string? reason, int? actorId, string? actorName) => new()
    {
        Action = action,
        FromValue = from,
        ToValue = to,
        Reason = reason,
        ActorUserId = actorId,
        ActorName = actorName,
        CreatedAt = DateTime.UtcNow,
    };

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
    // mọi phòng ban nhưng chỉ thấy phiếu được phân công cho mình — phiếu chưa phân công hoặc của kỹ thuật viên
    // khác trả 404 như thể không tồn tại (không lộ thông tin đồng nghiệp).
    private static bool IsTicketOutOfScope(
        MaintenanceTicket ticket, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        if (IsTechnician(currentUserRole))
        {
            return ticket.TechnicianId != currentUserId;
        }

        return IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, ticket.Asset.DepartmentId);
    }

    private static bool IsEmployee(string? currentUserRole) =>
        string.Equals(currentUserRole, EmployeeRoleName, StringComparison.Ordinal);

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
        ReportedByUserId = t.ReportedByUserId,
        ReportedByName = t.ReportedBy?.FullName,
        ReportedDate = t.ReportedDate,
        ResolvedDate = t.ResolvedDate,
        Notes = t.Notes,
        Version = t.Version,
        ResolutionHours = t.ResolvedDate.HasValue ? (t.ResolvedDate.Value - t.ReportedDate).TotalHours : null,
    };
}
