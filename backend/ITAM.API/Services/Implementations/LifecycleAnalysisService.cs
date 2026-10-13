using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ITAM.API.Services.Implementations;

// UC-16: Service lấy dữ liệu, áp ngưỡng và ghép DTO; logic tính thuần nằm trong LifecycleCalculator.
public class LifecycleAnalysisService : ILifecycleAnalysisService
{
    public const int MinMaxAgeYears = 1;
    public const int MaxMaxAgeYears = 30;
    public const int MinMaxFailureCount = 1;
    public const int MaxMaxFailureCount = 100;
    public const string DefaultSortBy = "score";
    public const int TicketDescriptionMaxLength = 200;

    private static readonly string[] AgeBucketLabels =
    {
        "< 1 năm",
        "1–3 năm",
        "3–5 năm",
        "≥ 5 năm",
        "Không rõ ngày mua",
    };

    private readonly ILifecycleRepository _repository;
    private readonly LifecycleOptions _options;

    public LifecycleAnalysisService(
        ILifecycleRepository repository,
        IOptions<LifecycleOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    // HỢP ĐỒNG VỚI MODULE DỰ BÁO NGÂN SÁCH: kiểu trả về và các trường base giữ nguyên.
    public async Task<IReadOnlyList<AssetReplacementCandidateDto>> GetReplacementCandidatesAsync(
        int? departmentId,
        int horizonYear)
    {
        var today = Today();
        if (horizonYear < today.Year)
        {
            return Array.Empty<AssetReplacementCandidateDto>();
        }

        var recommendations = await BuildRecommendationsAsync(departmentId, horizonYear, today);

        return recommendations
            .OrderByDescending(candidate => candidate.IsOverdueNow)
            .ThenByDescending(candidate => candidate.IsHighPriority)
            .ThenByDescending(candidate => candidate.AgeYears ?? -1d)
            .ThenBy(candidate => candidate.AssetCode, StringComparer.Ordinal)
            .Cast<AssetReplacementCandidateDto>()
            .ToList();
    }

    public async Task<PagedResultDto<ReplacementRecommendationDto>> GetCurrentReplacementCandidatesAsync(
        int? departmentId,
        int? categoryId,
        string sortBy,
        int page,
        int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        sortBy = string.IsNullOrWhiteSpace(sortBy) ? DefaultSortBy : sortBy.Trim();
        var validSortBy = ValidateSortBy(sortBy);   // sortBy sai → 400 trước khi tốn công truy vấn
        var today = Today();

        var recommendations = await BuildRecommendationsAsync(departmentId, today.Year, today);
        IEnumerable<ReplacementRecommendationDto> filtered = recommendations
            .Where(item => item.IsOverdueNow)
            .Where(item => !categoryId.HasValue || item.CategoryId == categoryId.Value);

        var sorted = SortRecommendations(filtered, validSortBy).ToList();

        return new PagedResultDto<ReplacementRecommendationDto>
        {
            Items = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = sorted.Count,
        };
    }

    public async Task<PagedResultDto<UpcomingReplacementDto>> GetUpcomingAsync(
        int? departmentId,
        int? categoryId,
        int months,
        int page,
        int pageSize)
    {
        if (months is < 1 or > 60)
        {
            throw new ArgumentException("Số tháng cảnh báo phải từ 1 đến 60.", nameof(months));
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);
        var today = Today();
        var evaluated = await EvaluateAllAsync(departmentId, today);

        var upcomingItems = evaluated
            .Where(item => !item.Evaluation.IsOverdueNow)
            .Where(item => !categoryId.HasValue || item.Row.CategoryId == categoryId.Value)
            .Select(item =>
            {
                var upcoming = LifecycleCalculator.EvaluateUpcoming(
                    item.Row,
                    item.MaxAgeYears,
                    item.MaxFailureCount,
                    months,
                    today);

                return (Item: item, Upcoming: upcoming);
            })
            .Where(x => x.Upcoming.AgeDueSoon || x.Upcoming.NearFailureThreshold)
            .Select(x => ToUpcomingDto(x.Item, x.Upcoming, today))
            .OrderBy(item => item.DaysUntilAgeDue.HasValue ? 0 : 1)
            .ThenBy(item => item.DaysUntilAgeDue ?? int.MaxValue)
            .ThenBy(item => item.AssetCode, StringComparer.Ordinal)
            .ToList();

        return new PagedResultDto<UpcomingReplacementDto>
        {
            Items = upcomingItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = upcomingItems.Count,
        };
    }

    // M2: chi tiết vòng đời một tài sản, dùng cùng Evaluate/ngưỡng hiệu lực/điểm ưu tiên như danh sách.
    public async Task<AssetLifecycleDetailDto> GetAssetDetailAsync(int assetId)
    {
        var row = await _repository.GetAssetLifecycleRowAsync(assetId)
            ?? throw new LifecycleAssetNotFoundException(assetId);

        var today = Today();
        var item = EvaluateRow(row, await LoadPolicyMapAsync(), today);
        var tickets = await _repository.GetTicketsByAssetAsync(assetId);
        var ageYears = LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today);

        return new AssetLifecycleDetailDto
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
            WarrantyExpiry = row.WarrantyExpiry,
            IsUnderWarranty = IsUnderWarranty(row, today),
            TicketCount = row.TicketCount,
            FailedTicketCount = row.FailedTicketCount,
            AppliedMaxAgeYears = item.MaxAgeYears,
            AppliedMaxFailureCount = item.MaxFailureCount,
            AgeDueDate = row.PurchaseDate?.AddYears(item.MaxAgeYears),
            IsOverdueNow = item.Evaluation.IsOverdueNow,
            IsHighPriority = item.Evaluation.IsHighPriority,
            DueYear = item.Evaluation.DueYear,
            Reasons = item.Evaluation.Reasons,
            PriorityScore = ScoreOf(item, ageYears),
            ReasonText = ReasonTextOf(item, ageYears),
            Tickets = tickets
                .Select(ticket => new AssetTicketSummaryDto
                {
                    Id = ticket.Id,
                    ReportedDate = ticket.ReportedDate,
                    Status = ticket.Status,
                    ResolvedDate = ticket.ResolvedDate,
                    Description = Truncate(ticket.Description, TicketDescriptionMaxLength),
                })
                .ToList(),
        };
    }

    public async Task<LifecycleStatsDto> GetStatsAsync(int? departmentId)
    {
        var today = Today();
        var evaluated = await EvaluateAllAsync(departmentId, today);

        if (evaluated.Count == 0)
        {
            return new LifecycleStatsDto
            {
                TotalAssets = 0,
                AssetsWithoutPurchaseDate = 0,
                AverageAgeYears = null,
                MedianAgeYears = null,
                AssetsWithAtLeastOneTicket = 0,
                FailureRatio = 0,
                AverageTicketsPerAsset = 0,
                OverdueNowCount = 0,
                ByCategory = Array.Empty<LifecycleCategoryStatsDto>(),
                AgeBuckets = Array.Empty<LifecycleAgeBucketDto>(),
                ByDepartment = Array.Empty<LifecycleDepartmentStatsDto>(),
                TopFailingAssets = Array.Empty<TopFailingAssetDto>(),
                TicketTrend = Array.Empty<TicketTrendPointDto>(),
            };
        }

        var ticketTrendRaw = await _repository.GetTicketCountByYearAsync(departmentId, today.Year - 4);

        var byCategory = evaluated
            .GroupBy(item => new { item.Row.CategoryId, item.Row.CategoryName })
            .OrderBy(group => group.Key.CategoryName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group.ToList();
                var withTicket = items.Count(item => item.Row.TicketCount > 0);
                var thresholds = items[0];

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
            MedianAgeYears = MedianAge(evaluated.Select(item => item.Row), today),
            AssetsWithAtLeastOneTicket = assetsWithTicket,
            FailureRatio = (double)assetsWithTicket / total,
            AverageTicketsPerAsset = (double)evaluated.Sum(item => item.Row.TicketCount) / total,
            OverdueNowCount = evaluated.Count(item => item.Evaluation.IsOverdueNow),
            ByCategory = byCategory,
            AgeBuckets = BuildAgeBuckets(evaluated.Select(item => item.Row), today),
            ByDepartment = BuildDepartmentStats(evaluated, today),
            TopFailingAssets = BuildTopFailingAssets(evaluated, today),
            TicketTrend = BuildTicketTrend(ticketTrendRaw, today.Year),
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

    private async Task<List<EvaluatedAsset>> EvaluateAllAsync(int? departmentId, DateOnly today)
    {
        var rows = await _repository.GetAssetLifecycleRowsAsync(departmentId);
        var policyByCategory = await LoadPolicyMapAsync();

        return rows.Select(row => EvaluateRow(row, policyByCategory, today)).ToList();
    }

    // Một nơi duy nhất áp ngưỡng hiệu lực và gọi Evaluate (danh sách, thống kê và chi tiết một tài sản cùng dùng).
    private EvaluatedAsset EvaluateRow(
        AssetLifecycleRow row,
        IReadOnlyDictionary<int, AssetCategoryLifecyclePolicy> policyByCategory,
        DateOnly today)
    {
        var thresholds = ResolveThresholds(row.CategoryId, policyByCategory);
        var evaluation = LifecycleCalculator.Evaluate(row, thresholds.MaxAgeYears, thresholds.MaxFailureCount, today);
        return new EvaluatedAsset(row, evaluation, thresholds.MaxAgeYears, thresholds.MaxFailureCount);
    }

    private static bool IsUnderWarranty(AssetLifecycleRow row, DateOnly today) =>
        row.WarrantyExpiry.HasValue && row.WarrantyExpiry.Value >= today;   // L6: chỉ là thông tin

    private static int ScoreOf(EvaluatedAsset item, double? ageYears) =>
        LifecycleCalculator.ComputePriorityScore(ageYears, item.MaxAgeYears, item.Row.TicketCount, item.MaxFailureCount);

    private static string ReasonTextOf(EvaluatedAsset item, double? ageYears) =>
        LifecycleCalculator.BuildReasonText(
            item.Evaluation.Reasons.Contains(LifecycleCalculator.ReasonAgeExceeded),
            item.Evaluation.Reasons.Contains(LifecycleCalculator.ReasonFailureCountExceeded),
            ageYears,
            item.MaxAgeYears,
            item.Row.TicketCount,
            item.MaxFailureCount);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    // Một đường duy nhất xác định ai là ứng viên và dựng DTO mở rộng; API hợp đồng và API giao diện dùng chung.
    private async Task<List<ReplacementRecommendationDto>> BuildRecommendationsAsync(
        int? departmentId,
        int horizonYear,
        DateOnly today)
    {
        var evaluated = await EvaluateAllAsync(departmentId, today);
        return evaluated
            .Where(item => item.Evaluation.DueYear.HasValue && item.Evaluation.DueYear.Value <= horizonYear)
            .Select(item => ToRecommendation(item, today))
            .ToList();
    }

    private async Task<Dictionary<int, AssetCategoryLifecyclePolicy>> LoadPolicyMapAsync() =>
        (await _repository.GetPoliciesAsync()).ToDictionary(policy => policy.CategoryId);

    private (int MaxAgeYears, int MaxFailureCount) ResolveThresholds(
        int categoryId,
        IReadOnlyDictionary<int, AssetCategoryLifecyclePolicy> policies) =>
        policies.TryGetValue(categoryId, out var policy)
            ? (policy.MaxAgeYears, policy.MaxFailureCount)
            : (_options.DefaultMaxAgeYears, _options.DefaultMaxFailureCount);

    private static ReplacementRecommendationDto ToRecommendation(EvaluatedAsset item, DateOnly today)
    {
        var row = item.Row;
        var ageYears = LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today);

        return new ReplacementRecommendationDto
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
            PriorityScore = ScoreOf(item, ageYears),
            ReasonText = ReasonTextOf(item, ageYears),
            WarrantyExpiry = row.WarrantyExpiry,
            IsUnderWarranty = IsUnderWarranty(row, today),
            AppliedMaxAgeYears = item.MaxAgeYears,
            AppliedMaxFailureCount = item.MaxFailureCount,
        };
    }

    private static UpcomingReplacementDto ToUpcomingDto(
        EvaluatedAsset item,
        LifecycleUpcomingEvaluation upcoming,
        DateOnly today) => new()
    {
        AssetId = item.Row.AssetId,
        AssetCode = item.Row.AssetCode,
        AssetName = item.Row.AssetName,
        CategoryId = item.Row.CategoryId,
        CategoryName = item.Row.CategoryName,
        DepartmentId = item.Row.DepartmentId,
        DepartmentName = item.Row.DepartmentName,
        AgeYears = LifecycleCalculator.ComputeAgeYears(item.Row.PurchaseDate, today),
        TicketCount = item.Row.TicketCount,
        AgeDueDate = upcoming.AgeDueDate,
        DaysUntilAgeDue = upcoming.DaysUntilAgeDue,
        Reasons = upcoming.Reasons,
        ReasonText = upcoming.ReasonText,
    };

    private static string ValidateSortBy(string sortBy) => sortBy switch
    {
        "score" or "age" or "failures" or "code" => sortBy,
        _ => throw new ArgumentException(
            "sortBy chỉ nhận một trong các giá trị: score, age, failures, code.",
            nameof(sortBy)),
    };

    private static IOrderedEnumerable<ReplacementRecommendationDto> SortRecommendations(
        IEnumerable<ReplacementRecommendationDto> candidates,
        string sortBy)
    {
        return sortBy switch
        {
            "score" => candidates
                .OrderByDescending(item => item.PriorityScore)
                .ThenBy(item => item.AgeYears.HasValue ? 0 : 1)
                .ThenByDescending(item => item.AgeYears ?? double.MinValue)
                .ThenBy(item => item.AssetCode, StringComparer.Ordinal),
            "age" => candidates
                .OrderBy(item => item.AgeYears.HasValue ? 0 : 1)
                .ThenByDescending(item => item.AgeYears ?? double.MinValue)
                .ThenBy(item => item.AssetCode, StringComparer.Ordinal),
            "failures" => candidates
                .OrderByDescending(item => item.TicketCount)
                .ThenBy(item => item.AssetCode, StringComparer.Ordinal),
            "code" => candidates
                .OrderBy(item => item.AssetCode, StringComparer.Ordinal),
            _ => throw new ArgumentException(
                "sortBy chỉ nhận một trong các giá trị: score, age, failures, code.",
                nameof(sortBy)),
        };
    }

    private static double? AverageAge(IEnumerable<AssetLifecycleRow> rows, DateOnly today)
    {
        var ages = rows
            .Select(row => LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today))
            .Where(age => age.HasValue)
            .Select(age => age!.Value)
            .ToList();

        return ages.Count == 0 ? null : ages.Average();
    }

    private static double? MedianAge(IEnumerable<AssetLifecycleRow> rows, DateOnly today)
    {
        var ages = rows
            .Select(row => LifecycleCalculator.ComputeAgeYears(row.PurchaseDate, today))
            .Where(age => age.HasValue)
            .Select(age => age!.Value)
            .OrderBy(age => age)
            .ToList();

        if (ages.Count == 0)
        {
            return null;
        }

        var middle = ages.Count / 2;
        return ages.Count % 2 == 1
            ? ages[middle]
            : (ages[middle - 1] + ages[middle]) / 2d;
    }

    private static IReadOnlyList<LifecycleAgeBucketDto> BuildAgeBuckets(
        IEnumerable<AssetLifecycleRow> rows,
        DateOnly today)
    {
        var buckets = Enumerable.Range(0, AgeBucketLabels.Length)
            .Select(_ => new List<AssetLifecycleRow>())
            .ToArray();

        foreach (var row in rows)
        {
            buckets[LifecycleCalculator.GetAgeBucketIndex(row.PurchaseDate, today)].Add(row);
        }

        return buckets
            .Select((bucket, index) =>
            {
                var withTicket = bucket.Count(row => row.TicketCount > 0);
                return new LifecycleAgeBucketDto
                {
                    Label = AgeBucketLabels[index],
                    Count = bucket.Count,
                    AssetsWithAtLeastOneTicket = withTicket,
                    FailureRatio = bucket.Count == 0 ? 0 : (double)withTicket / bucket.Count,
                    AverageTicketsPerAsset = bucket.Count == 0 ? 0 : (double)bucket.Sum(row => row.TicketCount) / bucket.Count,
                };
            })
            .ToList();
    }

    private static IReadOnlyList<LifecycleDepartmentStatsDto> BuildDepartmentStats(
        IEnumerable<EvaluatedAsset> evaluated,
        DateOnly today) =>
        evaluated
            .GroupBy(item => new { item.Row.DepartmentId, item.Row.DepartmentName })
            .OrderBy(group => group.Key.DepartmentName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group.ToList();
                var withTicket = items.Count(item => item.Row.TicketCount > 0);
                return new LifecycleDepartmentStatsDto
                {
                    DepartmentId = group.Key.DepartmentId,
                    DepartmentName = group.Key.DepartmentName,
                    AssetCount = items.Count,
                    AverageAgeYears = AverageAge(items.Select(item => item.Row), today),
                    AssetsWithAtLeastOneTicket = withTicket,
                    FailureRatio = (double)withTicket / items.Count,
                    OverdueNowCount = items.Count(item => item.Evaluation.IsOverdueNow),
                };
            })
            .ToList();

    private static IReadOnlyList<TopFailingAssetDto> BuildTopFailingAssets(IEnumerable<EvaluatedAsset> evaluated, DateOnly today) =>
        evaluated
            .Where(item => item.Row.TicketCount >= 1)
            .OrderByDescending(item => item.Row.TicketCount)
            .ThenByDescending(item => item.Row.FailedTicketCount)
            .ThenBy(item => item.Row.AssetCode, StringComparer.Ordinal)
            .Take(10)
            .Select(item => new TopFailingAssetDto
            {
                AssetId = item.Row.AssetId,
                AssetCode = item.Row.AssetCode,
                AssetName = item.Row.AssetName,
                CategoryName = item.Row.CategoryName,
                DepartmentName = item.Row.DepartmentName,
                TicketCount = item.Row.TicketCount,
                FailedTicketCount = item.Row.FailedTicketCount,
                AgeYears = LifecycleCalculator.ComputeAgeYears(item.Row.PurchaseDate, today),
            })
            .ToList();

    private static IReadOnlyList<TicketTrendPointDto> BuildTicketTrend(
        IReadOnlyList<TicketTrendPointDto> raw,
        int currentYear)
    {
        var counts = raw.ToDictionary(point => point.Year, point => point.TicketCount);
        return Enumerable.Range(currentYear - 4, 5)
            .Select(year => new TicketTrendPointDto
            {
                Year = year,
                TicketCount = counts.TryGetValue(year, out var count) ? count : 0,
            })
            .ToList();
    }
}
