using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IDisposalRequestRepository
{
    // Trả entity đang được theo dõi (tracked) kèm đủ navigation — Service sửa trực tiếp rồi SaveChangesAsync.
    Task<DisposalRequest?> GetByIdWithDetailsAsync(int id);

    Task<(List<DisposalRequest> Items, int TotalItems)> GetPagedAsync(
        int? departmentId, int? assetId, string? statusCode, int? technicianUserId, int page, int pageSize);

    // Tài sản Hỏng (Broken) chưa có phiếu thanh lý đang mở; departmentId lọc theo phòng ban của tài sản.
    Task<(List<Asset> Items, int TotalItems)> GetDisposalCandidatesAsync(int? departmentId, int page, int pageSize);

    // Tài sản đã có phiếu chưa kết thúc (chưa Rejected/Completed) hay chưa.
    Task<bool> HasOpenRequestForAssetAsync(int assetId);

    Task AddAsync(DisposalRequest request);
    Task<int> SaveChangesAsync();
}
