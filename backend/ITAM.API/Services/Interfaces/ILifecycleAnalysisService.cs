using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Services.Interfaces;

public interface ILifecycleAnalysisService
{
    Task<IReadOnlyList<AssetReplacementCandidateDto>> GetReplacementCandidatesAsync(int? departmentId, int horizonYear);
    Task<PagedResultDto<AssetReplacementCandidateDto>> GetCurrentReplacementCandidatesAsync(
        int? departmentId,
        int? categoryId,
        int page,
        int pageSize);
    Task<LifecycleStatsDto> GetStatsAsync(int? departmentId);
    Task<IReadOnlyList<LifecyclePolicyDto>> GetPoliciesAsync();
    Task<LifecyclePolicyDto> UpsertPolicyAsync(int categoryId, UpsertLifecyclePolicyRequestDto dto);
    Task DeletePolicyAsync(int categoryId);
}
