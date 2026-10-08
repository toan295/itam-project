using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Disposals;

namespace ITAM.API.Services.Interfaces;

public interface IDisposalRequestService
{
    Task<PagedResultDto<DisposalRequestResponseDto>> GetPagedAsync(
        int? departmentId, int? assetId, string? statusCode, int page, int pageSize,
        string? currentUserRole, int? currentUserDepartmentId, int currentUserId);

    // Tài sản đang ở trạng thái Hỏng và chưa có phiếu thanh lý đang mở — "hàng đợi" để Technician kiểm tra/đề xuất.
    Task<PagedResultDto<DisposalCandidateDto>> GetCandidatesAsync(
        int page, int pageSize, string? currentUserRole, int? currentUserDepartmentId);

    Task<DisposalRequestResponseDto> GetByIdAsync(
        int id, string? currentUserRole, int? currentUserDepartmentId, int currentUserId);

    // Bước 1 (Technician): kiểm tra tài sản, tạo phiếu ở trạng thái "Đã kiểm tra".
    Task<DisposalRequestResponseDto> CreateAsync(CreateDisposalRequestDto dto, int currentUserId);

    // Bước 2 (Technician): đề xuất thanh lý -> "Đã đề xuất".
    Task<DisposalRequestResponseDto> ProposeAsync(int id, ProposeDisposalRequestDto dto, int currentUserId);

    // Bước 3 (Manager phòng ban của tài sản): duyệt -> "Đã duyệt" hoặc từ chối -> "Từ chối".
    Task<DisposalRequestResponseDto> ApproveAsync(
        int id, ReviewDisposalRequestDto dto, int currentUserId, int? currentUserDepartmentId);
    Task<DisposalRequestResponseDto> RejectAsync(
        int id, ReviewDisposalRequestDto dto, int currentUserId, int? currentUserDepartmentId);

    // Bước 4 (Admin IT): thực hiện thanh lý -> "Hoàn tất", tài sản chuyển Disposed.
    Task<DisposalRequestResponseDto> CompleteAsync(int id, CompleteDisposalRequestDto dto, int currentUserId);

    // Admin IT gán/gỡ trạng thái phụ (trạng thái do Admin thêm) — không ảnh hưởng luồng.
    Task<DisposalRequestResponseDto> SetSubStatusAsync(int id, int? subStatusId, int currentUserId);
}
