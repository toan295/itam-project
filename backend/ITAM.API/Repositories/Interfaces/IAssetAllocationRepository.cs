using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Models;

namespace ITAM.API.Repositories.Interfaces;

public interface IAssetAllocationRepository
{
    Task<AssetAllocation?> GetByIdWithDetailsAsync(int id); // AsNoTracking — chỉ để đọc/map DTO.

    // TRACKED (không AsNoTracking) và Include(Asset) — dùng cho ReturnAsync: allocation và asset của nó
    // cùng được track bởi 1 AppDbContext, cho phép Service sửa cả 2 rồi ghi bằng đúng 1 SaveChangesAsync,
    // đúng quy tắc UC-15 "phải nằm trong cùng 1 transaction".
    Task<AssetAllocation?> GetEntityByIdAsync(int id);
    Task<bool> HasOpenAllocationAsync(int assetId);
    Task<AssetAllocation?> GetOpenAllocationByAssetIdAsync(int assetId);
    Task<(List<AssetAllocation> Items, int TotalItems)> GetPagedAsync(
        int? departmentId,
        AllocationStatus? status,
        int page,
        int pageSize);

    // Dữ liệu thô (allocation đang mở + ngày bảo trì gần nhất của tài sản) — Service tự quyết định
    // "quá hạn" theo thresholdDays (Repository chỉ đọc dữ liệu, không chứa nghiệp vụ).
    Task<List<AllocationOverdueCandidateRow>> GetOpenAllocationCandidatesAsync(int? departmentId);
    Task AddAsync(AssetAllocation allocation);
    Task<int> SaveChangesAsync();
}
