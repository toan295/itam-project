using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public interface IAssetRepository
{
    Task<Asset?> GetByIdWithDetailsAsync(int id);
    Task<Asset?> GetByAssetCodeAsync(string assetCode);
    // excludeAssetId: bỏ qua chính tài sản đang sửa. So khớp không phân biệt hoa/thường (collation của MySQL).
    Task<bool> SerialNumberExistsAsync(string serialNumber, int? excludeAssetId = null);
    Task AddAsync(Asset asset);
    void Update(Asset asset);
    Task<int> SaveChangesAsync();
    Task<bool> CategoryExistsAsync(int categoryId);
    Task<bool> DepartmentExistsAsync(int departmentId);

    // status đã được Service parse & validate trước — Repository chỉ lọc dữ liệu, không chứa nghiệp vụ.
    // orAssignedTechnicianId (UC-08 E2): khi có giá trị, bộ lọc phòng ban trở thành "phòng ban HOẶC tài sản
    // có phiếu bảo trì đang chờ xử lý được giao cho kỹ thuật viên này" — kỹ thuật viên phải thấy được tài sản
    // của phiếu mình phụ trách dù khác phòng ban.
    Task<(List<Asset> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, AssetStatus? status, int? orAssignedTechnicianId, int page, int pageSize);

    // Tài sản có phiếu bảo trì Pending đang được giao cho kỹ thuật viên này (dùng cho GET /assets/{id}).

    // UC-08: tìm kiếm/lọc nâng cao. Các tham số enum/bool đã được Service parse & validate trước.
    Task<(List<Asset> Items, int TotalItems)> SearchAsync(
        string? keyword,
        int? departmentId,
        int? categoryId,
        AssetStatus? status,
        int? purchaseYear,
        bool? isUnderWarranty,
        int? orAssignedTechnicianId,
        int page,
        int pageSize);
}
