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
        int pageSize);

    Task<PagedResultDto<UpcomingReplacementDto>> GetUpcomingAsync(
        int? departmentId,
        int? categoryId,
        int months,
        int page,
        int pageSize);

    Task<LifecycleStatsDto> GetStatsAsync(int? departmentId);
    Task<IReadOnlyList<LifecyclePolicyDto>> GetPoliciesAsync();
    Task<LifecyclePolicyDto> UpsertPolicyAsync(int categoryId, UpsertLifecyclePolicyRequestDto dto);
    Task DeletePolicyAsync(int categoryId);
}
