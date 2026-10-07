using ITAM.API.Configurations;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ITAM.API.Services.Implementations;

public class LifecycleAnalysisService : ILifecycleAnalysisService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly ILifecycleRepository _repository;
    private readonly LifecycleOptions _options;

    public LifecycleAnalysisService(
        ILifecycleRepository repository,
        IOptions<LifecycleOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<AssetReplacementCandidateDto>> GetReplacementCandidatesAsync(
        int? departmentId,
        int horizonYear)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (horizonYear < today.Year)
        {
            return Array.Empty<AssetReplacementCandidateDto>();
        }

        var rows = await _repository.GetAssetLifecycleRowsAsync(departmentId);
        var policies = await _repository.GetPoliciesAsync();
        var policyByCategory = policies.ToDictionary(policy => policy.CategoryId);

        var result = new List<AssetReplacementCandidateDto>();
        foreach (var row in rows)
        {
            var thresholds = ResolveThresholds(row.CategoryId, policyByCategory);
            var evaluation = LifecycleCalculator.Evaluate(
                row,
                thresholds.MaxAgeYears,
                thresholds.MaxFailureCount,
                today);

            if (!evaluation.DueYear.HasValue || evaluation.DueYear.Value > horizonYear)
            {
                continue;
            }

            var ageYears = LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today);
            result.Add(new AssetReplacementCandidateDto
            {
                AssetId = row.AssetId,
                AssetCode = row.AssetCode,
                AssetName = row.AssetName,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                DepartmentId = row.DepartmentId,
                DepartmentName = row.DepartmentName,
                PurchaseDate = row.PurchaseDate,
                AgeYears = ageYears,
                TicketCount = row.TicketCount,
                FailedTicketCount = row.FailedTicketCount,
                FailureRatePerYear = row.TicketCount / Math.Max(ageYears ?? 0d, 1d),
                MaxAgeYears = thresholds.MaxAgeYears,
                MaxFailureCount = thresholds.MaxFailureCount,
                Reasons = evaluation.Reasons,
                IsOverdueNow = evaluation.IsOverdueNow,
                Priority = evaluation.Priority,
                DueYear = evaluation.DueYear,
            });
        }

        return result
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueYear)
            .ThenByDescending(item => item.FailedTicketCount)
            .ThenByDescending(item => item.AgeYears ?? -1)
            .ThenBy(item => item.AssetCode)
            .ToList();
    }

    public async Task<PagedResultDto<AssetReplacementCandidateDto>> GetCurrentReplacementCandidatesAsync(
        int? departmentId,
        int? categoryId,
        int page,
        int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var currentYear = DateOnly.FromDateTime(DateTime.UtcNow).Year;
        var candidates = await GetReplacementCandidatesAsync(departmentId, currentYear);
        var filtered = candidates
            .Where(item => item.IsOverdueNow)
            .Where(item => !categoryId.HasValue || item.CategoryId == categoryId.Value)
            .ToList();

        return new PagedResultDto<AssetReplacementCandidateDto>
        {
            Items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = filtered.Count,
        };
    }

    public async Task<LifecycleStatsDto> GetStatsAsync(int? departmentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await _repository.GetAssetLifecycleRowsAsync(departmentId);
        var policies = await _repository.GetPoliciesAsync();
        var policyByCategory = policies.ToDictionary(policy => policy.CategoryId);

        var evaluations = rows.ToDictionary(
            row => row.AssetId,
            row =>
            {
                var thresholds = ResolveThresholds(row.CategoryId, policyByCategory);
                return LifecycleCalculator.Evaluate(
                    row,
                    thresholds.MaxAgeYears,
                    thresholds.MaxFailureCount,
                    today);
            });

        var knownAges = rows
            .Select(row => LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today))
            .Where(age => age.HasValue)
            .Select(age => age!.Value)
            .ToList();

        var byCategory = rows
            .GroupBy(row => new { row.CategoryId, row.CategoryName })
            .OrderBy(group => group.Key.CategoryName)
            .Select(group =>
            {
                var groupRows = group.ToList();
                var thresholds = ResolveThresholds(group.Key.CategoryId, policyByCategory);
                var groupAges = groupRows
                    .Select(row => LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today))
                    .Where(age => age.HasValue)
                    .Select(age => age!.Value)
                    .ToList();

                return new LifecycleCategoryStatsDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    TotalAssets = groupRows.Count,
                    AverageAgeYears = groupAges.Count == 0 ? null : groupAges.Average(),
                    FailureRatio = groupRows.Count == 0
                        ? 0
                        : groupRows.Count(row => row.TicketCount > 0) / (double)groupRows.Count,
                    MaxAgeYears = thresholds.MaxAgeYears,
                    MaxFailureCount = thresholds.MaxFailureCount,
                    OverdueNowCount = groupRows.Count(row => evaluations[row.AssetId].IsOverdueNow),
                };
            })
            .ToList();

        return new LifecycleStatsDto
        {
            TotalAssets = rows.Count,
            AverageAgeYears = knownAges.Count == 0 ? null : knownAges.Average(),
            FailureRatio = rows.Count == 0 ? 0 : rows.Count(row => row.TicketCount > 0) / (double)rows.Count,
            AverageTicketsPerAsset = rows.Count == 0 ? 0 : rows.Sum(row => row.TicketCount) / (double)rows.Count,
            OverdueNowCount = rows.Count(row => evaluations[row.AssetId].IsOverdueNow),
            UnknownPurchaseDateCount = rows.Count(row => !row.PurchaseDate.HasValue),
            ByCategory = byCategory,
            AgeBuckets = BuildAgeBuckets(rows, today),
        };
    }

    public async Task<IReadOnlyList<LifecyclePolicyDto>> GetPoliciesAsync()
    {
        var categories = await _repository.GetCategoriesAsync();
        var policies = await _repository.GetPoliciesAsync();
        var policyByCategory = policies.ToDictionary(policy => policy.CategoryId);

        return categories
            .Select(category =>
            {
                var thresholds = ResolveThresholds(category.Id, policyByCategory);
                return new LifecyclePolicyDto
                {
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    MaxAgeYears = thresholds.MaxAgeYears,
                    MaxFailureCount = thresholds.MaxFailureCount,
                    IsOverridden = policyByCategory.ContainsKey(category.Id),
                };
            })
            .ToList();
    }

    public async Task<LifecyclePolicyDto> UpsertPolicyAsync(
        int categoryId,
        UpsertLifecyclePolicyRequestDto dto)
    {
        var category = await _repository.GetCategoryByIdAsync(categoryId)
            ?? throw new AssetCategoryNotFoundException(categoryId);

        var policy = await _repository.GetPolicyByCategoryIdAsync(categoryId);
        if (policy is null)
        {
            policy = new AssetCategoryLifecyclePolicy
            {
                CategoryId = categoryId,
                MaxAgeYears = dto.MaxAgeYears,
                MaxFailureCount = dto.MaxFailureCount,
            };
            await _repository.AddPolicyAsync(policy);
        }
        else
        {
            policy.MaxAgeYears = dto.MaxAgeYears;
            policy.MaxFailureCount = dto.MaxFailureCount;
        }

        await _repository.SaveChangesAsync();

        return new LifecyclePolicyDto
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            MaxAgeYears = policy.MaxAgeYears,
            MaxFailureCount = policy.MaxFailureCount,
            IsOverridden = true,
        };
    }

    public async Task DeletePolicyAsync(int categoryId)
    {
        var policy = await _repository.GetPolicyByCategoryIdAsync(categoryId)
            ?? throw new LifecyclePolicyNotFoundException(categoryId);

        _repository.RemovePolicy(policy);
        await _repository.SaveChangesAsync();
    }

    private (int MaxAgeYears, int MaxFailureCount) ResolveThresholds(
        int categoryId,
        IReadOnlyDictionary<int, AssetCategoryLifecyclePolicy> policies)
    {
        if (policies.TryGetValue(categoryId, out var policy))
        {
            return (policy.MaxAgeYears, policy.MaxFailureCount);
        }

        return (_options.DefaultMaxAgeYears, _options.DefaultMaxFailureCount);
    }

    private static IReadOnlyList<LifecycleAgeBucketDto> BuildAgeBuckets(
        IReadOnlyList<AssetLifecycleRow> rows,
        DateOnly today)
    {
        var lessThanOne = 0;
        var oneToThree = 0;
        var threeToFive = 0;
        var fiveOrMore = 0;
        var unknown = 0;

        foreach (var row in rows)
        {
            var age = LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today);
            if (!age.HasValue)
            {
                unknown++;
            }
            else if (age.Value < 1)
            {
                lessThanOne++;
            }
            else if (age.Value < 3)
            {
                oneToThree++;
            }
            else if (age.Value < 5)
            {
                threeToFive++;
            }
            else
            {
                fiveOrMore++;
            }
        }

        return new List<LifecycleAgeBucketDto>
        {
            new() { Label = "< 1 năm", Count = lessThanOne },
            new() { Label = "1–3 năm", Count = oneToThree },
            new() { Label = "3–5 năm", Count = threeToFive },
            new() { Label = "≥ 5 năm", Count = fiveOrMore },
            new() { Label = "Không rõ ngày mua", Count = unknown },
        };
    }
}
