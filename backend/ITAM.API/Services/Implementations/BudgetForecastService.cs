using System.Text.Json;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.Extensions.Logging;

namespace ITAM.API.Services.Implementations;

// UC-17: Dự báo thay thế & ngân sách. Service KHÔNG tự tính tuổi/số lần lỗi — chỉ nhân số lượng ứng viên
// (do ILifecycleAnalysisService xác định) với đơn giá tham khảo theo loại. Mọi phép tính tiền dùng decimal.
public class BudgetForecastService : IBudgetForecastService
{
    // Khớp HasMaxLength(500) của BudgetForecast.Notes trong AppDbContext.
    public const int NotesMaxLength = 500;
    public const string NoRatioWarning = "Chưa cấu hình định mức thiết bị/người — không tính được nhu cầu mua thêm.";
    private const int MaxForecastYearsAhead = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IBudgetForecastRepository _repo;
    private readonly ILifecycleAnalysisService _lifecycle;
    private readonly ILogger<BudgetForecastService> _logger;

    public BudgetForecastService(
        IBudgetForecastRepository repo,
        ILifecycleAnalysisService lifecycle,
        ILogger<BudgetForecastService> logger)
    {
        _repo = repo;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public async Task<ForecastGenerationResultDto> GenerateAsync(GenerateForecastRequestDto dto)
    {
        var currentYear = DateTime.UtcNow.Year;
        if (dto.Year < currentYear || dto.Year > currentYear + MaxForecastYearsAhead)
            throw new ArgumentException($"Năm dự báo phải từ {currentYear} đến {currentYear + MaxForecastYearsAhead}.");

        // 1. Phòng ban cần có dòng dự báo (D8).
        List<(int Id, string Name)> departments;
        if (dto.DepartmentId.HasValue)
        {
            var department = await _repo.GetDepartmentAsync(dto.DepartmentId.Value)
                ?? throw new ArgumentException($"Phòng ban (DepartmentId={dto.DepartmentId}) không tồn tại.");
            departments = new List<(int Id, string Name)> { department };
        }
        else
        {
            departments = await _repo.GetAllDepartmentsAsync();
        }

        // 2. Ứng viên từ module Vòng đời; CHỈ giữ DueYear == năm dự báo để không cộng dồn giữa các năm (D6).
        var all = await _lifecycle.GetReplacementCandidatesAsync(dto.DepartmentId, dto.Year);
        var candidatesByDepartment = all
            .Where(c => c.DueYear == dto.Year)
            .ToLookup(c => c.DepartmentId);

        // 3. Đơn giá theo loại (D7).
        var priceByCategory = (await _repo.GetAllPricesAsync()).ToDictionary(p => p.CategoryId, p => p.UnitPrice);

        // 3b. Kế hoạch mở rộng, định mức, tên loại: nạp MỘT lần trước vòng lặp phòng ban (không truy vấn trong vòng lặp).
        var plans = await _repo.GetPlansAsync(dto.Year, dto.DepartmentId);
        var ratios = await _repo.GetAllRatiosAsync();
        var categoryNames = (await _repo.GetAllCategoriesAsync()).ToDictionary(c => c.Id, c => c.Name);
        var headcountByDepartment = plans.ToDictionary(p => p.DepartmentId, p => p.AdditionalHeadcount);

        var missingPriceByCategory = new Dictionary<string, int>();
        var staged = new List<(BudgetForecast Entity, string DepartmentName, List<ForecastBreakdownItemDto> Breakdown,
            int Headcount, List<ExpansionBreakdownItemDto> Expansion)>();
        var now = DateTime.UtcNow;

        foreach (var (departmentId, departmentName) in departments)
        {
            var candidates = candidatesByDepartment[departmentId].ToList();

            var breakdown = candidates
                .GroupBy(c => (c.CategoryId, c.CategoryName))
                .Select(g =>
                {
                    var count = g.Count();
                    var hasPrice = priceByCategory.TryGetValue(g.Key.CategoryId, out var unitPrice);
                    return new ForecastBreakdownItemDto
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName,
                        AssetCount = count,
                        UnitPrice = hasPrice ? unitPrice : null,
                        Subtotal = hasPrice ? count * unitPrice : null,
                        Assets = g.OrderBy(c => c.AssetCode, StringComparer.OrdinalIgnoreCase).Select(c => new ForecastAssetItemDto
                        {
                            AssetId = c.AssetId,
                            AssetCode = c.AssetCode,
                            AssetName = c.AssetName,
                            PurchaseDate = c.PurchaseDate,
                            AgeYears = c.AgeYears,
                            TicketCount = c.TicketCount,
                            MaxAgeYears = c.MaxAgeYears,
                            MaxFailureCount = c.MaxFailureCount,
                            Reasons = c.Reasons.ToList(),
                        }).ToList(),
                    };
                })
                .OrderBy(b => b.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 3c. Mua thêm (E1): ⌈nhân sự tăng × định mức⌉ — int × decimal → decimal, Ceiling trên decimal (tuyệt đối không qua double).
            var headcount = headcountByDepartment.GetValueOrDefault(departmentId);   // E6: không có kế hoạch → 0
            var expansion = ratios
                .Select(r =>
                {
                    var quantity = (int)Math.Ceiling(headcount * r.DevicesPerPerson);
                    var hasPrice = priceByCategory.TryGetValue(r.CategoryId, out var unitPrice);
                    return new ExpansionBreakdownItemDto
                    {
                        CategoryId = r.CategoryId,
                        CategoryName = categoryNames.GetValueOrDefault(r.CategoryId) ?? $"Loại #{r.CategoryId}",
                        DevicesPerPerson = r.DevicesPerPerson,
                        Quantity = quantity,
                        UnitPrice = hasPrice ? unitPrice : null,
                        Subtotal = hasPrice ? quantity * unitPrice : null,
                    };
                })
                .Where(i => i.Quantity > 0)
                .OrderBy(i => i.CategoryName, StringComparer.Ordinal)
                .ToList();

            // Một loại thiếu giá chỉ sinh MỘT cảnh báo, cộng số thiết bị của cả thay thế lẫn mua thêm (E5).
            foreach (var item in breakdown.Where(b => b.UnitPrice is null))
                missingPriceByCategory[item.CategoryName] =
                    missingPriceByCategory.GetValueOrDefault(item.CategoryName) + item.AssetCount;
            foreach (var item in expansion.Where(i => i.UnitPrice is null))
                missingPriceByCategory[item.CategoryName] =
                    missingPriceByCategory.GetValueOrDefault(item.CategoryName) + item.Quantity;

            // 4. Upsert theo (Năm, Phòng ban) — chạy lại không sinh dòng trùng (D8).
            var entity = await _repo.GetByYearAndDepartmentAsync(dto.Year, departmentId);
            var isNew = entity is null;
            if (isNew)
            {
                entity = new BudgetForecast { Year = dto.Year, DepartmentId = departmentId };
                await _repo.AddAsync(entity);
            }
            else if (entity!.IsDeleted)
            {
                // Dòng từng bị xoá mềm: tạo lại cùng (năm, phòng ban) thì khôi phục dòng cũ (unique index không cho thêm dòng mới).
                entity.IsDeleted = false;
                entity.DeletedAt = null;
            }

            entity!.EstimatedReplacementCount = candidates.Count;                  // đếm cả loại thiếu giá
            entity.EstimatedBudget = breakdown.Sum(b => b.Subtotal ?? 0m);        // chỉ loại có giá
            entity.Notes = BuildMissingPriceNote(breakdown, expansion);
            entity.BreakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions);
            // E8: EstimatedBudget (cột cũ) vẫn CHỈ là thay thế; mua thêm nằm ở 3 cột mới. Dòng mới luôn ghi object (không null).
            entity.EstimatedExpansionCount = expansion.Sum(i => i.Quantity);
            entity.EstimatedExpansionBudget = expansion.Sum(i => i.Subtotal ?? 0m);
            entity.ExpansionBreakdownJson = JsonSerializer.Serialize(
                new ExpansionBreakdownEnvelope { AdditionalHeadcount = headcount, Items = expansion }, JsonOptions);
            entity.GeneratedAt = now;
            if (!isNew) _repo.Update(entity);

            staged.Add((entity, departmentName, breakdown, headcount, expansion));
        }

        // Một lần lưu duy nhất cho mọi dòng (Unit-of-Work).
        await _repo.SaveChangesAsync();

        var warnings = missingPriceByCategory
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"Loại '{kv.Key}': {kv.Value} thiết bị chưa có đơn giá tham khảo — không tính vào ngân sách.")
            .ToList();
        // E6: có nhập nhân sự nhưng chưa cấu hình định mức nào → kết quả mua thêm = 0 sẽ gây hiểu nhầm nếu im lặng.
        if (ratios.Count == 0 && plans.Any(p => p.AdditionalHeadcount > 0))
            warnings.Add(NoRatioWarning);

        // 5. Map DTO sau khi lưu để dòng mới đã có Id.
        return new ForecastGenerationResultDto
        {
            Year = dto.Year,
            Forecasts = staged.Select(s => ToResponse(s.Entity, s.DepartmentName, s.Breakdown, s.Headcount, s.Expansion)).ToList(),
            Warnings = warnings,
        };
    }

    public async Task<PagedResultDto<BudgetForecastResponseDto>> GetPagedAsync(
        int? year, int? departmentId, int page, int pageSize)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, total) = await _repo.GetPagedAsync(year, departmentId, page, pageSize);

        return new PagedResultDto<BudgetForecastResponseDto>
        {
            Items = items.Select(f => ToResponse(f, f.Department.Name, ParseBreakdown(f))).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
        };
    }

    public async Task<BudgetForecastResponseDto> GetByIdAsync(int id)
    {
        var forecast = await _repo.GetByIdWithDetailsAsync(id)
            ?? throw new BudgetForecastNotFoundException(id);
        return ToResponse(forecast, forecast.Department.Name, ParseBreakdown(forecast));
    }

    // Xoá mềm: chỉ đặt cờ, dòng vẫn nằm trong DB.
    public async Task DeleteAsync(int id)
    {
        var forecast = await _repo.GetTrackedByIdAsync(id)
            ?? throw new BudgetForecastNotFoundException(id);

        forecast.IsDeleted = true;
        forecast.DeletedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync();
    }

    public async Task<List<ReferencePriceDto>> GetPricesAsync()
    {
        var categories = await _repo.GetAllCategoriesAsync();
        var prices = (await _repo.GetAllPricesAsync()).ToDictionary(p => p.CategoryId, p => p.UnitPrice);

        return categories.Select(c => new ReferencePriceDto
        {
            CategoryId = c.Id,
            CategoryName = c.Name,
            UnitPrice = prices.TryGetValue(c.Id, out var price) ? price : null,
        }).ToList();
    }

    public async Task<ReferencePriceDto> UpsertPriceAsync(int categoryId, UpsertReferencePriceRequestDto dto)
    {
        // Service tự kiểm tra lại vì có thể được gọi từ nơi khác ngoài Controller.
        if (dto.UnitPrice <= 0 || dto.UnitPrice > UpsertReferencePriceRequestValidator.MaxUnitPrice)
            throw new ArgumentException("Đơn giá phải lớn hơn 0 và không vượt quá giới hạn cho phép.");

        var categories = await _repo.GetAllCategoriesAsync();
        var category = categories.FirstOrDefault(c => c.Id == categoryId);
        if (category == default)
            throw new AssetCategoryNotFoundException(categoryId);

        var price = await _repo.GetPriceAsync(categoryId);   // gồm cả dòng đã xoá mềm
        if (price is null)
        {
            price = new AssetCategoryReferencePrice { CategoryId = categoryId, UnitPrice = dto.UnitPrice };
            await _repo.AddPriceAsync(price);
        }
        else
        {
            price.UnitPrice = dto.UnitPrice;
            price.IsDeleted = false;      // nhập lại đơn giá cho loại từng bị xoá mềm = khôi phục
            price.DeletedAt = null;
            _repo.UpdatePrice(price);
        }

        await _repo.SaveChangesAsync();

        return new ReferencePriceDto { CategoryId = categoryId, CategoryName = category.Name, UnitPrice = price.UnitPrice };
    }

    public async Task DeletePriceAsync(int categoryId)
    {
        var price = await _repo.GetPriceAsync(categoryId);
        if (price is null || price.IsDeleted)
            throw new ReferencePriceNotFoundException(categoryId);

        // Xoá mềm: giữ dòng trong DB, chỉ ẩn khỏi tính toán và giao diện.
        price.IsDeleted = true;
        price.DeletedAt = DateTime.UtcNow;
        _repo.UpdatePrice(price);
        await _repo.SaveChangesAsync();
    }

    // ---- nội bộ ----

    // "Thiếu đơn giá: Máy in (3 thiết bị); Switch (1 thiết bị)" hoặc null; cắt cho vừa cột Notes để tránh DbUpdateException.
    internal static string? BuildMissingPriceNote(
        IEnumerable<ForecastBreakdownItemDto> breakdown, IEnumerable<ExpansionBreakdownItemDto>? expansion = null)
    {
        // Gộp thay thế + mua thêm theo loại để mỗi loại thiếu giá chỉ xuất hiện một lần trong ghi chú.
        var missing = breakdown.Where(b => b.UnitPrice is null).Select(b => (b.CategoryName, Count: b.AssetCount))
            .Concat((expansion ?? Enumerable.Empty<ExpansionBreakdownItemDto>())
                .Where(i => i.UnitPrice is null).Select(i => (i.CategoryName, Count: i.Quantity)))
            .GroupBy(m => m.CategoryName)
            .Select(g => (CategoryName: g.Key, Count: g.Sum(m => m.Count)))
            .OrderBy(m => m.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count == 0) return null;

        var text = "Thiếu đơn giá: " + string.Join("; ", missing.Select(b => $"{b.CategoryName} ({b.Count} thiết bị)"));
        return text.Length <= NotesMaxLength ? text : text[..(NotesMaxLength - 1)] + "…";
    }

    private List<ForecastBreakdownItemDto> ParseBreakdown(BudgetForecast forecast)
    {
        if (string.IsNullOrWhiteSpace(forecast.BreakdownJson)) return new List<ForecastBreakdownItemDto>();

        try
        {
            return JsonSerializer.Deserialize<List<ForecastBreakdownItemDto>>(forecast.BreakdownJson, JsonOptions)
                   ?? new List<ForecastBreakdownItemDto>();
        }
        catch (JsonException ex)
        {
            // JSON hỏng không được làm hỏng cả trang lịch sử: trả danh sách rỗng và ghi cảnh báo.
            _logger.LogWarning(ex, "BreakdownJson của dự báo Id={ForecastId} không đọc được.", forecast.Id);
            return new List<ForecastBreakdownItemDto>();
        }
    }

    // Dòng cũ (tạo trước Tuần 8) có ExpansionBreakdownJson = null → mua thêm 0; JSON hỏng → rỗng + cảnh báo, không ném.
    private ExpansionBreakdownEnvelope ParseExpansion(BudgetForecast forecast)
    {
        if (string.IsNullOrWhiteSpace(forecast.ExpansionBreakdownJson)) return new ExpansionBreakdownEnvelope();

        try
        {
            return JsonSerializer.Deserialize<ExpansionBreakdownEnvelope>(forecast.ExpansionBreakdownJson, JsonOptions)
                   ?? new ExpansionBreakdownEnvelope();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ExpansionBreakdownJson của dự báo Id={ForecastId} không đọc được.", forecast.Id);
            return new ExpansionBreakdownEnvelope();
        }
    }

    private BudgetForecastResponseDto ToResponse(BudgetForecast f, string departmentName, List<ForecastBreakdownItemDto> breakdown)
    {
        var expansion = ParseExpansion(f);
        return ToResponse(f, departmentName, breakdown, expansion.AdditionalHeadcount, expansion.Items ?? new());
    }

    private static BudgetForecastResponseDto ToResponse(
        BudgetForecast f, string departmentName, List<ForecastBreakdownItemDto> breakdown,
        int additionalHeadcount, List<ExpansionBreakdownItemDto> expansion) => new()
    {
        Id = f.Id,
        Year = f.Year,
        DepartmentId = f.DepartmentId,
        DepartmentName = departmentName,
        EstimatedReplacementCount = f.EstimatedReplacementCount,
        EstimatedBudget = f.EstimatedBudget,
        Notes = f.Notes,
        GeneratedAt = f.GeneratedAt,
        Breakdown = breakdown,
        AdditionalHeadcount = additionalHeadcount,
        EstimatedExpansionCount = f.EstimatedExpansionCount,
        EstimatedExpansionBudget = f.EstimatedExpansionBudget,
        ExpansionBreakdown = expansion,
        TotalPurchaseCount = f.EstimatedReplacementCount + f.EstimatedExpansionCount,
        TotalBudget = f.EstimatedBudget + f.EstimatedExpansionBudget,
        PurchaseSummary = BuildPurchaseSummary(breakdown, expansion),
    };

    // Danh sách mua sắm: hợp nhất thay thế + mua thêm theo CategoryId; sắp theo tên loại bằng Ordinal (không phụ thuộc culture).
    public static List<PurchaseSummaryItemDto> BuildPurchaseSummary(
        IEnumerable<ForecastBreakdownItemDto> breakdown, IEnumerable<ExpansionBreakdownItemDto> expansion)
    {
        var byCategory = new Dictionary<int, PurchaseSummaryItemDto>();

        foreach (var b in breakdown)
        {
            byCategory[b.CategoryId] = new PurchaseSummaryItemDto
            {
                CategoryId = b.CategoryId,
                CategoryName = b.CategoryName,
                ReplacementQuantity = b.AssetCount,
                UnitPrice = b.UnitPrice,
            };
        }

        foreach (var e in expansion)
        {
            if (!byCategory.TryGetValue(e.CategoryId, out var item))
            {
                item = new PurchaseSummaryItemDto { CategoryId = e.CategoryId, CategoryName = e.CategoryName };
                byCategory[e.CategoryId] = item;
            }
            item.ExpansionQuantity = e.Quantity;
            if (e.UnitPrice is not null) item.UnitPrice = e.UnitPrice;   // lệch do dữ liệu cũ → ưu tiên mục mua thêm
        }

        foreach (var item in byCategory.Values)
        {
            item.TotalQuantity = item.ReplacementQuantity + item.ExpansionQuantity;
            item.TotalCost = item.UnitPrice.HasValue ? item.TotalQuantity * item.UnitPrice.Value : null;
        }

        return byCategory.Values.OrderBy(i => i.CategoryName, StringComparer.Ordinal).ToList();
    }

    // Định dạng ExpansionBreakdownJson: MỘT object { additionalHeadcount, items[] }, không phải mảng trơn.
    private sealed class ExpansionBreakdownEnvelope
    {
        public int AdditionalHeadcount { get; set; }
        public List<ExpansionBreakdownItemDto>? Items { get; set; } = new();
    }
}
