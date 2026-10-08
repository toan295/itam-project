using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class AssetService : IAssetService
{
    private const string AdminRoleName = "Admin IT";
    private const string ManagerRoleName = "Manager";
    private const int NoAccessSentinelDepartmentId = -1; // DepartmentId không tồn tại -> query luôn trả rỗng.
    private static readonly string[] DepartmentScopedRoles = { ManagerRoleName };

    private readonly IAssetRepository _repo;
    private readonly IMaintenanceTicketRepository _ticketRepo;

    public AssetService(IAssetRepository repo, IMaintenanceTicketRepository ticketRepo)
    {
        _repo = repo;
        _ticketRepo = ticketRepo;
    }

    public async Task<AssetResponseDto> CreateAsync(
        CreateAssetRequestDto dto, string? currentUserRole, int? currentUserDepartmentId)
    {
        // UC-05 E3: Manager chỉ được thêm tài sản cho đúng phòng ban mình phụ trách.
        if (IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, dto.DepartmentId))
        {
            throw new DepartmentForbiddenException();
        }

        await EnsureCategoryAndDepartmentExistAsync(dto.CategoryId, dto.DepartmentId);

        var assetCode = dto.AssetCode.Trim();

        // UC-05 E1: AssetCode đã tồn tại -> 409 Conflict.
        var existed = await _repo.GetByAssetCodeAsync(assetCode);
        if (existed is not null)
        {
            throw new AssetCodeAlreadyExistsException(assetCode);
        }

        var serialNumber = NormalizeSerialNumber(dto.SerialNumber);
        if (serialNumber is not null && await _repo.SerialNumberExistsAsync(serialNumber))
        {
            throw new AssetSerialNumberAlreadyExistsException(serialNumber);
        }

        var asset = new Asset
        {
            AssetCode = assetCode,
            Name = dto.Name.Trim(),
            CategoryId = dto.CategoryId,
            SerialNumber = serialNumber,
            Specification = dto.Specification,
            OperatingSystem = dto.OperatingSystem,
            DepartmentId = dto.DepartmentId,
            Status = AssetStatus.InUse, // Mặc định InUse đúng UC-05 bước 4.
            PurchaseDate = dto.PurchaseDate,
            WarrantyExpiry = dto.WarrantyExpiry,
            CreatedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(asset);
        await SaveChangesGuardingAssetCodeConflictAsync(assetCode);

        var created = await _repo.GetByIdWithDetailsAsync(asset.Id)
            ?? throw new AssetNotFoundException(asset.Id);
        return MapToDto(created);
    }

    public async Task<AssetResponseDto> UpdateAsync(
        int id, UpdateAssetRequestDto dto, string? currentUserRole, int? currentUserDepartmentId)
    {
        var asset = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new AssetNotFoundException(id);

        // UC-06 E2: Manager không được sửa tài sản ngoài phòng ban phụ trách, kể cả khi
        // muốn chuyển tài sản sang một phòng ban khác không phải của mình.
        if (IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, asset.DepartmentId)
            || IsManagerOutsideDepartment(currentUserRole, currentUserDepartmentId, dto.DepartmentId))
        {
            throw new DepartmentForbiddenException();
        }

        if (!TryParseAssetStatus(dto.Status, out var status))
        {
            throw new ArgumentException(
                $"Status không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<AssetStatus>())}");
        }

        // Thanh lý phải đi qua quy trình riêng (Technician kiểm tra -> đề xuất -> Manager duyệt -> Admin IT
        // thực hiện, xem DisposalRequestService) — không ai, kể cả Admin IT, được đặt Status = Disposed
        // trực tiếp qua Update thường. Chỉ chặn khi đây là hành động chuyển trạng thái (asset chưa Disposed),
        // để vẫn sửa được các trường khác của tài sản đã thanh lý từ trước.
        if (status == AssetStatus.Disposed && asset.Status != AssetStatus.Disposed)
        {
            throw new AssetDisposalNotAllowedException();
        }

        // Chiều ngược lại của UC-07: tài sản đã thanh lý (do Admin IT) chỉ Admin IT mới được khôi phục —
        // nếu không, Manager có thể "hồi sinh" tài sản qua PUT và vô hiệu hoá quyết định thanh lý.
        if (asset.Status == AssetStatus.Disposed
            && status != AssetStatus.Disposed
            && !string.Equals(currentUserRole, AdminRoleName, StringComparison.Ordinal))
        {
            throw new AssetReactivationNotAllowedException();
        }

        await EnsureCategoryAndDepartmentExistAsync(dto.CategoryId, dto.DepartmentId);

        var assetCode = dto.AssetCode.Trim();
        var assetCodeChanged = !string.Equals(assetCode, asset.AssetCode, StringComparison.Ordinal);

        // UC-06 bước 3: nếu đổi AssetCode thì phải kiểm tra trùng mã với tài sản khác.
        if (assetCodeChanged)
        {
            var existed = await _repo.GetByAssetCodeAsync(assetCode);
            if (existed is not null)
            {
                throw new AssetCodeAlreadyExistsException(assetCode);
            }
        }

        // Chỉ kiểm tra khi serial thực sự đổi, để dữ liệu cũ đã trùng không chặn việc sửa các trường khác.
        var serialNumber = NormalizeSerialNumber(dto.SerialNumber);
        var serialChanged = !string.Equals(serialNumber, asset.SerialNumber, StringComparison.OrdinalIgnoreCase);
        if (serialChanged && serialNumber is not null && await _repo.SerialNumberExistsAsync(serialNumber, asset.Id))
        {
            throw new AssetSerialNumberAlreadyExistsException(serialNumber);
        }

        var previousStatus = asset.Status;

        asset.AssetCode = assetCode;
        asset.Name = dto.Name.Trim();
        asset.CategoryId = dto.CategoryId;
        asset.SerialNumber = serialNumber;
        asset.Specification = dto.Specification;
        asset.OperatingSystem = dto.OperatingSystem;
        asset.DepartmentId = dto.DepartmentId;
        asset.Status = status;
        asset.PurchaseDate = dto.PurchaseDate;
        asset.WarrantyExpiry = dto.WarrantyExpiry;
        // TODO: ghi AuditLog (OldValue/NewValue) khi AuditLogService được xây dựng (chưa có ở Tuần 3-4).

        _repo.Update(asset);
        await SyncMaintenanceTicketsAsync(asset.Id, previousStatus, status);
        await SaveChangesGuardingAssetCodeConflictAsync(assetCode);

        // Đọc lại để nạp đúng Category/Department mới (response cần tên loại/phòng ban sau khi đổi).
        var updated = await _repo.GetByIdWithDetailsAsync(asset.Id) ?? throw new AssetNotFoundException(asset.Id);
        return MapToDto(updated);
    }

    // Giữ trang Bảo trì khớp với trạng thái tài sản khi người dùng đổi trạng thái tay:
    //  - chuyển sang "Bảo trì": tài sản phải có phiếu chờ xử lý -> tự tạo nếu chưa có;
    //  - rời khỏi "Bảo trì" (về Đang dùng/Hỏng): phiếu đang chờ không còn ý nghĩa -> tự đóng (Đang dùng = Đã xử lý, Hỏng = Không xử lý được).
    // Mọi thay đổi nằm chung 1 DbContext với tài sản nên được lưu atomic cùng lần SaveChanges của UpdateAsync.
    private async Task SyncMaintenanceTicketsAsync(int assetId, AssetStatus previous, AssetStatus next)
    {
        if (previous != AssetStatus.Maintenance && next == AssetStatus.Maintenance)
        {
            if (!await _ticketRepo.HasPendingTicketAsync(assetId))
            {
                await _ticketRepo.AddAsync(new MaintenanceTicket
                {
                    AssetId = assetId,
                    IssueDescription = "Tài sản được chuyển sang trạng thái Bảo trì (phiếu tạo tự động từ trang Tài sản). Vui lòng cập nhật nội dung bảo trì.",
                    Status = TicketStatus.Pending,
                    Priority = TicketPriority.Normal,
                    ReportedDate = DateTime.UtcNow,
                });
            }
        }
        else if (previous == AssetStatus.Maintenance && next != AssetStatus.Maintenance)
        {
            var closeAs = next == AssetStatus.Broken ? TicketStatus.Failed : TicketStatus.Resolved;
            foreach (var ticket in await _ticketRepo.GetPendingByAssetAsync(assetId))
            {
                ticket.Status = closeAs;
                ticket.ResolvedDate = DateTime.UtcNow;
                ticket.Notes = $"Đóng tự động: tài sản được đổi trạng thái sang {(next == AssetStatus.Broken ? "Hỏng" : "Đang sử dụng")} từ trang Tài sản.";
            }
        }
    }

    public async Task<AssetResponseDto> GetByIdAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var asset = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new AssetNotFoundException(id);

        // Cùng quy tắc phạm vi phòng ban với danh sách/tìm kiếm (UC-08 E2) — nếu không kiểm tra ở
        // đây, Manager/Technician có thể "lách" việc bị lọc khỏi danh sách bằng cách đoán id trực
        // tiếp qua GET /assets/{id}. Trả 404 (như thể không tồn tại) thay vì 403, để không lộ việc
        // asset đó có thực sự tồn tại hay không cho người ngoài phòng ban.
        if (IsOutsideDepartmentScope(currentUserRole, currentUserDepartmentId, asset.DepartmentId))
        {
            throw new AssetNotFoundException(id);
        }

        return MapToDto(asset);
    }

    public async Task<PagedResultDto<AssetResponseDto>> GetPagedAsync(
        int? departmentId, string? status, int page, int pageSize,
        string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var parsedStatus = ParseStatusOrThrow(status);
        var scopedDepartmentId = ResolveDepartmentScope(departmentId, currentUserRole, currentUserDepartmentId);

        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, total) = await _repo.GetPagedAsync(
            scopedDepartmentId, parsedStatus, orAssignedTechnicianId: null, page, pageSize);
        return new PagedResultDto<AssetResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<PagedResultDto<AssetResponseDto>> SearchAsync(
        AssetSearchFilterDto filter, string? currentUserRole, int? currentUserDepartmentId, int? currentUserId)
    {
        var parsedStatus = ParseStatusOrThrow(filter.Status);
        var isUnderWarranty = ParseWarrantyStatusOrThrow(filter.WarrantyStatus);
        var scopedDepartmentId = ResolveDepartmentScope(filter.DepartmentId, currentUserRole, currentUserDepartmentId);

        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize);

        var (items, total) = await _repo.SearchAsync(
            filter.Keyword, scopedDepartmentId, filter.CategoryId, parsedStatus,
            filter.PurchaseYear, isUnderWarranty, orAssignedTechnicianId: null,
            page, pageSize);

        return new PagedResultDto<AssetResponseDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    private static AssetStatus? ParseStatusOrThrow(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!TryParseAssetStatus(status, out var st))
        {
            throw new ArgumentException(
                $"Status không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<AssetStatus>())}");
        }

        return st;
    }

    // Enum.TryParse mặc định chấp nhận cả chuỗi số ("3" -> Disposed) — không đúng ý định thiết kế
    // (Status dùng string tên để dễ đọc/debug qua Postman, xem UpdateAssetRequestDto). Kiểm tra chặt
    // theo đúng tên định nghĩa trong enum, từ chối mọi chuỗi số hoặc alias khác.
    // Serial rỗng/chỉ có khoảng trắng coi như không có (null) để không bị tính trùng với nhau.
    private static string? NormalizeSerialNumber(string? serialNumber) =>
        string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();

    private static bool TryParseAssetStatus(string value, out AssetStatus status)
    {
        if (Enum.GetNames<AssetStatus>().Contains(value, StringComparer.Ordinal))
        {
            status = Enum.Parse<AssetStatus>(value);
            return true;
        }

        status = default;
        return false;
    }

    private static bool? ParseWarrantyStatusOrThrow(string? warrantyStatus)
    {
        if (string.IsNullOrWhiteSpace(warrantyStatus))
        {
            return null;
        }

        return warrantyStatus switch
        {
            "Valid" => true,
            "Expired" => false,
            _ => throw new ArgumentException("WarrantyStatus không hợp lệ. Phải là một trong: Valid, Expired"),
        };
    }

    // UC-08 E2: Manager tự động bị giới hạn theo phòng ban mình phụ trách — bỏ qua departmentId client
    // gửi lên (nếu có). Admin IT và Technician (phục vụ mọi phòng ban) không bị giới hạn.
    private static int? ResolveDepartmentScope(
        int? requestedDepartmentId, string? currentUserRole, int? currentUserDepartmentId)
    {
        if (!DepartmentScopedRoles.Contains(currentUserRole))
        {
            return requestedDepartmentId;
        }

        // Claim DepartmentId thiếu/hỏng -> không trả về dữ liệu nào (an toàn hơn là mặc định mở toàn bộ).
        return currentUserDepartmentId ?? NoAccessSentinelDepartmentId;
    }

    private async Task EnsureCategoryAndDepartmentExistAsync(int categoryId, int departmentId)
    {
        // UC-05 điều kiện trước: đã có danh mục loại tài sản & phòng ban (UC-04). Kiểm tra ở đây
        // để trả 400 rõ ràng thay vì để MySQL từ chối bằng lỗi ràng buộc khoá ngoại (500).
        if (!await _repo.CategoryExistsAsync(categoryId))
        {
            throw new ArgumentException($"Loại tài sản (CategoryId={categoryId}) không tồn tại.");
        }

        if (!await _repo.DepartmentExistsAsync(departmentId))
        {
            throw new ArgumentException($"Phòng ban (DepartmentId={departmentId}) không tồn tại.");
        }
    }

    private async Task SaveChangesGuardingAssetCodeConflictAsync(string assetCode)
    {
        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race hiếm: 2 request cùng lúc dùng chung AssetCode vượt qua bước kiểm tra trùng ở trên.
            // Unique index trên AssetCode (itam.dbml) là chốt chặn cuối cùng ở DB — ánh xạ lại thành
            // 409 thân thiện thay vì để lỗi 500 từ MySQL lộ ra ngoài.
            throw new AssetCodeAlreadyExistsException(assetCode);
        }
    }

    private static bool IsManagerOutsideDepartment(
        string? currentUserRole, int? currentUserDepartmentId, int targetDepartmentId) =>
        string.Equals(currentUserRole, ManagerRoleName, StringComparison.Ordinal)
        && currentUserDepartmentId != targetDepartmentId;

    // Dùng cho các thao tác chỉ-đọc (GetById): chỉ Manager bị giới hạn theo phòng ban.
    private static bool IsOutsideDepartmentScope(
        string? currentUserRole, int? currentUserDepartmentId, int targetDepartmentId) =>
        DepartmentScopedRoles.Contains(currentUserRole)
        && currentUserDepartmentId != targetDepartmentId;

    private static AssetResponseDto MapToDto(Asset a) => new()
    {
        Id = a.Id,
        AssetCode = a.AssetCode,
        Name = a.Name,
        CategoryId = a.CategoryId,
        CategoryName = a.Category?.Name ?? "",
        SerialNumber = a.SerialNumber,
        Specification = a.Specification,
        OperatingSystem = a.OperatingSystem,
        DepartmentId = a.DepartmentId,
        DepartmentName = a.Department?.Name ?? "",
        Status = a.Status.ToString(),
        PurchaseDate = a.PurchaseDate,
        WarrantyExpiry = a.WarrantyExpiry,
        IsUnderWarranty = a.WarrantyExpiry.HasValue && a.WarrantyExpiry.Value >= DateOnly.FromDateTime(DateTime.UtcNow),
    };
}
