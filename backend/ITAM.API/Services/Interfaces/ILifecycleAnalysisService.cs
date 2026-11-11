using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Services.Interfaces;

public interface ILifecycleAnalysisService
{
    // Hợp đồng với module Dự báo ngân sách — KHÔNG đổi kiểu trả về/ý nghĩa trường.
    Task<IReadOnlyList<AssetReplacementCandidateDto>> GetReplacementCandidatesAsync(int? departmentId, int horizonYear);

    Task<PagedResultDto<ReplacementRecommendationDto>> GetCurrentReplacementCandidatesAsync(
        int? departmentId,
        int? categoryId,
        string sortBy,
        int page,
        int pageSize,
        string? keyword = null,
        string? reason = null,
        bool? underWarranty = null,
        int? minScore = null);

    Task<PagedResultDto<UpcomingReplacementDto>> GetUpcomingAsync(
        int? departmentId,
        int? categoryId,
        int months,
        int page,
        int pageSize,
        string? keyword = null,
        string? reason = null,
        int? minTickets = null);

    // Dùng cho xuất báo cáo: TOÀN BỘ danh sách (không phân trang), cùng quy tắc và thứ tự với bản phân trang không bộ lọc nâng cao.
    // Thêm mới, KHÔNG đổi hợp đồng với module Dự báo.
    Task<IReadOnlyList<ReplacementRecommendationDto>> GetAllCurrentReplacementCandidatesAsync(int? departmentId, int? categoryId);
    Task<IReadOnlyList<UpcomingReplacementDto>> GetAllUpcomingAsync(int? departmentId, int? categoryId, int months);

    // M2: chi tiết một tài sản (404 nếu không tồn tại/đã thanh lý → LifecycleAssetNotFoundException).
    Task<AssetLifecycleDetailDto> GetAssetDetailAsync(int assetId);

    Task<LifecycleStatsDto> GetStatsAsync(int? departmentId, int? categoryId = null);
    Task<IReadOnlyList<LifecyclePolicyDto>> GetPoliciesAsync();
    Task<LifecyclePolicyDto> UpsertPolicyAsync(int categoryId, UpsertLifecyclePolicyRequestDto dto, int? actorUserId = null);
    // version: phiên bản cấu hình client đang thấy (bắt buộc); lệch -> LifecyclePolicyConflictException (409).
    Task DeletePolicyAsync(int categoryId, int? version = null);
}
