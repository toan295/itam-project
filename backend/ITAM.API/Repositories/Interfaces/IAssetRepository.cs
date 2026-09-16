using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public interface IAssetRepository
{
    Task<Asset?> GetByIdWithDetailsAsync(int id);
    Task<Asset?> GetByAssetCodeAsync(string assetCode);
    Task AddAsync(Asset asset);
    void Update(Asset asset);
    Task<int> SaveChangesAsync();
    Task<bool> CategoryExistsAsync(int categoryId);
    Task<bool> DepartmentExistsAsync(int departmentId);

    // status đã được Service parse & validate trước — Repository chỉ lọc dữ liệu, không chứa nghiệp vụ.
    Task<(List<Asset> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, AssetStatus? status, int page, int pageSize);

    // UC-08: tìm kiếm/lọc nâng cao. Các tham số enum/bool đã được Service parse & validate trước.
    Task<(List<Asset> Items, int TotalItems)> SearchAsync(
        string? keyword,
        int? departmentId,
        int? categoryId,
        AssetStatus? status,
        int? purchaseYear,
        bool? isUnderWarranty,
        int page,
        int pageSize);
}
