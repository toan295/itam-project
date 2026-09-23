using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;

namespace ITAM.API.Repositories.Interfaces;

public interface IAssetAllocationRepository
{
    Task<AssetAllocation?> GetByIdWithDetailsAsync(int id);
    Task<AssetAllocation?> GetEntityByIdAsync(int id);
    Task<bool> HasOpenAllocationAsync(int assetId);
    Task<AssetAllocation?> GetOpenAllocationByAssetIdAsync(int assetId);
    Task<(List<AssetAllocation> Items, int TotalItems)> GetPagedAsync(
        int? departmentId,
        AllocationStatus? status,
        int page,
        int pageSize);
    Task<IReadOnlyList<OverdueAllocationDto>> GetOverdueAsync(
        DateOnly today,
        int thresholdDays,
        int? departmentId);
    Task AddAsync(AssetAllocation allocation);
    Task<int> SaveChangesAsync();
}
