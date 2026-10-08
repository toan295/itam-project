using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ITAM.API.Services.Implementations;

// UC-16: phân tích vòng đời tài sản. Quy tắc tuổi/lỗi/DueYear nằm hết trong LifecycleCalculator;
// Service chỉ lấy dữ liệu, áp ngưỡng theo loại rồi ghép kết quả.
public class LifecycleAnalysisService : ILifecycleAnalysisService
{
    public const int MinMaxAgeYears = 1;
    public const int MaxMaxAgeYears = 30;
    public const int MinMaxFailureCount = 1;
    public const int MaxMaxFailureCount = 100;

    private readonly ILifecycleRepository _repository;
    private readonly LifecycleOptions _options;

    public LifecycleAnalysisService(
        ILifecycleRepository repository,
        IOptions<LifecycleOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    // HỢP ĐỒNG VỚI MODULE DỰ BÁO NGÂN SÁCH (ke-hoach-tuan7-lan2.md, Mục C.1).
    public async Task<IReadOnlyList<AssetReplacementCandidateDto>> GetReplacementCandidatesAsync(
        int? departmentId,
        int horizonYear)
    {
        var today = Today();
        if (horizonYear < today.Year)
        {
            return Array.Empty<AssetReplacementCandidateDto>();
        }

        var evaluated = await EvaluateAllAsync(departmentId, today);

        return evaluated
            .Where(item => item.Evaluation.DueYear.HasValue && item.Evaluation.DueYear.Value <= horizonYear)
            .Select(item => ToCandidate(item, today))
            .OrderByDescending(candidate => candidate.IsOverdueNow)
            .ThenByDescending(candidate => candidate.IsHighPriority)
            .ThenByDescending(candidate => candidate.AgeYears ?? -1d)
            .ThenBy(candidate => candidate.AssetCode, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<PagedResultDto<AssetReplacementCandidateDto>> GetCurrentReplacementCandidatesAsync(
        int? departmentId,
        int? categoryId,
        int page,
        int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        // UC-16: danh sách "Đề xuất thay thế" chỉ gồm tài sản ĐÃ vượt ngưỡng; tài sản sắp tới hạn thì không hiện.
        var candidates = await GetReplacementCandidatesAsync(departmentId, Today().Year);
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
        var today = Today();
        var evaluated = await EvaluateAllAsync(departmentId, today);
        var policyByCategory = await LoadPolicyMapAsync();

        var byCategory = evaluated
            .GroupBy(item => new { item.Row.CategoryId, item.Row.CategoryName })
            .OrderBy(group => group.Key.CategoryName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group.ToList();
                var thresholds = ResolveThresholds(group.Key.CategoryId, policyByCategory);
                var withTicket = items.Count(item => item.Row.TicketCount > 0);

                return new LifecycleCategoryStatsDto
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    AssetCount = items.Count,
                    AverageAgeYears = AverageAge(items.Select(item => item.Row), today),
                    AssetsWithAtLeastOneTicket = withTicket,
                    FailureRatio = (double)withTicket / items.Count,
                    MaxAgeYears = thresholds.MaxAgeYears,
                    MaxFailureCount = thresholds.MaxFailureCount,
                    OverdueNowCount = items.Count(item => item.Evaluation.IsOverdueNow),
                };
            })
            .ToList();

        var total = evaluated.Count;
        var assetsWithTicket = evaluated.Count(item => item.Row.TicketCount > 0);

        return new LifecycleStatsDto
        {
            TotalAssets = total,
            AssetsWithoutPurchaseDate = evaluated.Count(item => !item.Row.PurchaseDate.HasValue),
            AverageAgeYears = AverageAge(evaluated.Select(item => item.Row), today),
            AssetsWithAtLeastOneTicket = assetsWithTicket,
            FailureRatio = total == 0 ? 0 : (double)assetsWithTicket / total,
            AverageTicketsPerAsset = total == 0 ? 0 : (double)evaluated.Sum(item => item.Row.TicketCount) / total,
            OverdueNowCount = evaluated.Count(item => item.Evaluation.IsOverdueNow),
            ByCategory = byCategory,
            AgeBuckets = BuildAgeBuckets(evaluated.Select(item => item.Row), today),
        };
    }

    public async Task<IReadOnlyList<LifecyclePolicyDto>> GetPoliciesAsync()
    {
        var categories = await _repository.GetCategoriesAsync();
        var policyByCategory = await LoadPolicyMapAsync();

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
        // Service tự kiểm tra lại giới hạn vì có thể được gọi từ nơi khác ngoài Controller.
        if (dto.MaxAgeYears is < MinMaxAgeYears or > MaxMaxAgeYears)
        {
            throw new ArgumentException(
                $"Tuổi tối đa phải từ {MinMaxAgeYears} đến {MaxMaxAgeYears} năm.");
        }

        if (dto.MaxFailureCount is < MinMaxFailureCount or > MaxMaxFailureCount)
        {
            throw new ArgumentException(
                $"Số lần lỗi tối đa phải từ {MinMaxFailureCount} đến {MaxMaxFailureCount}.");
        }

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
            // Entity được repository trả về có tracking nên SaveChanges tự ghi nhận thay đổi.
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

    // ---- nội bộ ----

    private sealed record EvaluatedAsset(
        AssetLifecycleRow Row,
        LifecycleEvaluation Evaluation,
        int MaxAgeYears,
        int MaxFailureCount);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    // Lấy dữ liệu đúng 1 lần rồi đánh giá toàn bộ — không truy vấn DB theo từng tài sản.
    private async Task<List<EvaluatedAsset>> EvaluateAllAsync(int? departmentId, DateOnly today)
    {
        var rows = await _repository.GetAssetLifecycleRowsAsync(departmentId);
        var policyByCategory = await LoadPolicyMapAsync();

        return rows
            .Select(row =>
            {
                var thresholds = ResolveThresholds(row.CategoryId, policyByCategory);
                var evaluation = LifecycleCalculator.Evaluate(
                    row, thresholds.MaxAgeYears, thresholds.MaxFailureCount, today);
                return new EvaluatedAsset(row, evaluation, thresholds.MaxAgeYears, thresholds.MaxFailureCount);
            })
            .ToList();
    }

    private async Task<Dictionary<int, AssetCategoryLifecyclePolicy>> LoadPolicyMapAsync() =>
        (await _repository.GetPoliciesAsync()).ToDictionary(policy => policy.CategoryId);

    // D4: có dòng riêng thì dùng, không thì lấy mặc định từ cấu hình.
    private (int MaxAgeYears, int MaxFailureCount) ResolveThresholds(
        int categoryId,
        IReadOnlyDictionary<int, AssetCategoryLifecyclePolicy> policies) =>
        policies.TryGetValue(categoryId, out var policy)
            ? (policy.MaxAgeYears, policy.MaxFailureCount)
            : (_options.DefaultMaxAgeYears, _options.DefaultMaxFailureCount);

    // Chỉ tính tài sản CÓ PurchaseDate (D1); không có tài sản nào → null.
    private static double? AverageAge(IEnumerable<AssetLifecycleRow> rows, DateOnly today)
    {
        var ages = rows
            .Select(row => LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today))
            .Where(age => age.HasValue)
            .Select(age => age!.Value)
            .ToList();

        return ages.Count == 0 ? null : ages.Average();
    }

    private static AssetReplacementCandidateDto ToCandidate(EvaluatedAsset item, DateOnly today)
    {
        var row = item.Row;
        var ageYears = LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today);

        return new AssetReplacementCandidateDto
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
            MaxAgeYears = item.MaxAgeYears,
            MaxFailureCount = item.MaxFailureCount,
            Reasons = item.Evaluation.Reasons,
            IsOverdueNow = item.Evaluation.IsOverdueNow,
            IsHighPriority = item.Evaluation.IsHighPriority,
            DueYear = item.Evaluation.DueYear!.Value,
        };
    }

    private static IReadOnlyList<LifecycleAgeBucketDto> BuildAgeBuckets(
        IEnumerable<AssetLifecycleRow> rows,
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
