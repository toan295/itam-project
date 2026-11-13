// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using ITAM.API.Repositories.Models;
// using System.Security.Cryptography;
// using System.Text;
// using System.Text.Json;
// using ITAM.API.Helpers;
// using ITAM.API.Models.DTOs.Common;
// using ITAM.API.Models.DTOs.Forecasts;
// using ITAM.API.Models.DTOs.Lifecycle;
// using ITAM.API.Models.Entities;
// using ITAM.API.Repositories.Interfaces;
// using ITAM.API.Services.Interfaces;
// using ITAM.API.Validators;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace ITAM.API.Services.Implementations;
//
// // UC-17: Dự báo thay thế & ngân sách. Service KHÔNG tự tính tuổi/số lần lỗi — chỉ nhân số lượng ứng viên
// // (do ILifecycleAnalysisService xác định) với đơn giá tham khảo theo loại. Mọi phép tính tiền dùng decimal.
// //
// // Mô hình (xem docs/quy-tac-du-bao-ngan-sach.md):
// //   THAY THẾ  = tài sản của module Vòng đời có DueYear == năm dự báo; mỗi tài sản đúng một lần, thay 1:1 nên giữ nguyên năng lực.
// //   MUA THÊM  = phần THIẾU HỤT: max(0, ⌈(nhân viên hiện tại + thay đổi kế hoạch) × định mức⌉ − tài sản cùng loại đang dùng được).
// //   Hai phần không bao giờ trừ vào nhau: tồn kho chỉ đáp ứng nhu cầu mua thêm; thiết bị đến hạn không bị lấy ra khỏi phần thay thế.
// public class BudgetForecastService : IBudgetForecastService
// {
//     // Khớp HasMaxLength(500) của BudgetForecast.Notes trong AppDbContext.
//     public const int NotesMaxLength = 500;
//     public const string NoRatioWarning = "Chưa cấu hình định mức thiết bị/người — không tính được nhu cầu mua thêm.";
//     // Phiên bản công thức: đổi công thức thì dự báo cũ tự thành "không còn khớp" thay vì bị coi là không đổi.
//     public const int FormulaVersion = 2;
//     // decimal(18,2) tối đa 9.999.999.999.999.999,99 — vượt thì báo lỗi rõ ràng thay vì để DB từ chối (500).
//     public const decimal MaxStoredAmount = 9_999_999_999_999_999m;
//     private const int MaxForecastYearsAhead = 10;
//
//     private static readonly JsonSerializerOptions JsonOptions = new()
//     {
//         PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//         PropertyNameCaseInsensitive = true,
//     };
//
//     private readonly IBudgetForecastRepository _repo;
//     private readonly ILifecycleAnalysisService _lifecycle;
//     private readonly IExclusiveSection _exclusive;
//     private readonly ILogger<BudgetForecastService> _logger;
//
//     public BudgetForecastService(
//         IBudgetForecastRepository repo,
//         ILifecycleAnalysisService lifecycle,
//         IExclusiveSection exclusive,
//         ILogger<BudgetForecastService> logger)
//     {
//         _repo = repo;
//         _lifecycle = lifecycle;
//         _exclusive = exclusive;
//         _logger = logger;
//     }
//
//     // ---------------------------------------------------------------- tạo dự báo
//
//     public async Task<ForecastGenerationResultDto> GenerateAsync(GenerateForecastRequestDto dto)
//     {
//         var currentYear = VietnamTime.Today().Year;
//         if (dto.Year < currentYear || dto.Year > currentYear + MaxForecastYearsAhead)
//             throw new ArgumentException($"Năm dự báo phải từ {currentYear} đến {currentYear + MaxForecastYearsAhead}.");
//
//         if (dto.DepartmentId.HasValue && await _repo.GetDepartmentAsync(dto.DepartmentId.Value) is null)
//             throw new ArgumentException($"Phòng ban (DepartmentId={dto.DepartmentId}) không tồn tại.");
//
//         // Hai yêu cầu cùng năm chạy lần lượt (không đồng thời): yêu cầu sau thấy kết quả của yêu cầu trước nên không đụng unique index,
//         // và mọi lần đọc đầu vào nằm trong một transaction nên một lần chạy không trộn dữ liệu trước/sau khi người khác sửa.
//         return await _exclusive.RunAsync(LockTarget.ForecastYear, dto.Year, () => GenerateCoreAsync(dto));
//     }
//
//     private async Task<ForecastGenerationResultDto> GenerateCoreAsync(GenerateForecastRequestDto dto)
//     {
//         var inputs = await LoadInputsAsync(dto.Year, dto.DepartmentId);
//         var existingByDepartment = (await _repo.GetForecastsByYearAsync(dto.Year)).ToDictionary(f => f.DepartmentId);
//         var now = ForecastInputGuard.NowUtc();
//
//         var staged = new List<(BudgetForecast Entity, RowComputation Row, string Change, ForecastPreviousDto? Previous)>();
//         var warnings = new List<string>();
//
//         foreach (var (departmentId, departmentName) in inputs.Departments)
//         {
//             var row = ComputeRow(inputs, departmentId, departmentName);
//             warnings.AddRange(row.Warnings);
//
//             existingByDepartment.TryGetValue(departmentId, out var entity);
//             ForecastPreviousDto? previous = entity is { IsDeleted: false }
//                 ? new ForecastPreviousDto
//                 {
//                     Revision = entity.Revision,
//                     GeneratedAt = ForecastInputGuard.AsUtc(entity.GeneratedAt),
//                     TotalPurchaseCount = entity.EstimatedReplacementCount + entity.EstimatedExpansionCount,
//                     TotalBudget = entity.EstimatedBudget + entity.EstimatedExpansionBudget,
//                 }
//                 : null;
//
//             // Đầu vào y hệt lần trước => kết quả y hệt: không ghi gì (idempotent), không tăng Revision, giữ nguyên GeneratedAt.
//             if (entity is { IsDeleted: false } && entity.InputsHash == row.InputsHash)
//             {
//                 staged.Add((entity, row, "Unchanged", previous));
//                 continue;
//             }
//
//             string change;
//             if (entity is null)
//             {
//                 entity = new BudgetForecast { Year = dto.Year, DepartmentId = departmentId };
//                 await _repo.AddAsync(entity);
//                 change = "Created";
//             }
//             else if (entity.IsDeleted)
//             {
//                 // Dòng từng bị xoá mềm: tạo lại cùng (năm, phòng ban) thì khôi phục dòng cũ (unique index không cho thêm dòng mới).
//                 entity.IsDeleted = false;
//                 entity.DeletedAt = null;
//                 entity.Revision++;
//                 change = "Restored";
//             }
//             else
//             {
//                 entity.Revision++;
//                 change = "Updated";
//             }
//
//             Apply(entity, row, now);
//             staged.Add((entity, row, change, previous));
//         }
//
//         // Một lần lưu duy nhất cho mọi dòng (Unit-of-Work): lỗi giữa chừng thì không dòng nào được ghi.
//         try
//         {
//             await _repo.SaveChangesAsync();
//         }
//         catch (DbUpdateConcurrencyException)
//         {
//             throw new ForecastInputConflictException("Dự báo vừa được người khác thay đổi trong lúc tạo. Vui lòng thử lại.");
//         }
//
//         // Gộp cảnh báo thiếu đơn giá: mỗi loại một dòng cho cả lần chạy, cộng số thiết bị thay thế lẫn mua thêm (E5).
//         var missing = new Dictionary<string, int>();
//         foreach (var s in staged)
//         {
//             foreach (var b in s.Row.Breakdown.Where(b => b.UnitPrice is null))
//                 missing[b.CategoryName] = missing.GetValueOrDefault(b.CategoryName) + b.AssetCount;
//             foreach (var i in s.Row.Expansion.Where(i => i.UnitPrice is null && i.Quantity > 0))
//                 missing[i.CategoryName] = missing.GetValueOrDefault(i.CategoryName) + i.Quantity;
//         }
//
//         var result = new List<string>();
//         result.AddRange(missing.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
//             .Select(kv => $"Loại '{kv.Key}': {kv.Value} thiết bị chưa có đơn giá tham khảo — không tính vào ngân sách."));
//         // E6: có thay đổi nhân sự nhưng chưa cấu hình định mức nào → kết quả mua thêm = 0 sẽ gây hiểu nhầm nếu im lặng.
//         if (inputs.Ratios.Count == 0 && inputs.Plans.Values.Any(p => p.AdditionalHeadcount != 0))
//             result.Add(NoRatioWarning);
//         result.AddRange(warnings);
//
//         return new ForecastGenerationResultDto
//         {
//             Year = dto.Year,
//             Forecasts = staged.Select(s =>
//             {
//                 var response = ToResponse(s.Entity, s.Row.DepartmentName, s.Row.Breakdown, s.Row.Envelope);
//                 response.StaleStatus = "Current";   // vừa tính từ đầu vào hiện tại
//                 response.Change = s.Change;
//                 response.Previous = s.Previous;
//                 return response;
//             }).ToList(),
//             Warnings = result,
//         };
//     }
//
//     private static void Apply(BudgetForecast entity, RowComputation row, DateTime now)
//     {
//         var replacementBudget = row.Breakdown.Sum(b => b.Subtotal ?? 0m);
//         var expansionBudget = row.Expansion.Sum(i => i.Subtotal ?? 0m);
//         if (replacementBudget > MaxStoredAmount || expansionBudget > MaxStoredAmount)
//             throw new ArgumentException($"Ngân sách của phòng ban '{row.DepartmentName}' vượt giới hạn lưu trữ — kiểm tra lại đơn giá/định mức/nhân sự.");
//
//         entity.EstimatedReplacementCount = row.Breakdown.Sum(b => b.AssetCount);   // đếm cả loại thiếu giá
//         entity.EstimatedBudget = replacementBudget;                                // chỉ loại có giá
//         entity.Notes = BuildMissingPriceNote(row.Breakdown, row.Expansion);
//         entity.BreakdownJson = JsonSerializer.Serialize(row.Breakdown, JsonOptions);
//         // E8: EstimatedBudget (cột cũ) vẫn CHỈ là thay thế; mua thêm nằm ở 3 cột mới. Dòng mới luôn ghi object (không null).
//         entity.EstimatedExpansionCount = row.Expansion.Sum(i => i.Quantity);
//         entity.EstimatedExpansionBudget = expansionBudget;
//         entity.ExpansionBreakdownJson = JsonSerializer.Serialize(row.Envelope, JsonOptions);
//         entity.InputsJson = row.InputsJson;
//         entity.InputsHash = row.InputsHash;
//         entity.GeneratedAt = now;
//     }
//
//     // ---------------------------------------------------------------- đầu vào & tính từng dòng (dùng chung cho tạo dự báo và kiểm tra "đã cũ")
//
//     private sealed record ForecastInputs(
//         int Year,
//         List<(int Id, string Name)> Departments,
//         ILookup<int, AssetReplacementCandidateDto> DueByDepartment,      // DueYear == Year
//         Dictionary<int, int> CarryOverByDepartment,                      // DueYear < Year (đã tới hạn từ trước)
//         Dictionary<int, decimal> Prices,
//         Dictionary<int, string> CategoryNames,
//         Dictionary<int, DepartmentExpansionPlan> Plans,
//         List<AssetCategoryDeviceRatio> Ratios,
//         Dictionary<int, int> Baseline,
//         Dictionary<(int DepartmentId, int CategoryId), int> Supply,
//         Dictionary<int, int> NoPurchaseDate);
//
//     // Tất cả truy vấn đầu vào nằm ở đây, mỗi nguồn đúng một lần — không truy vấn trong vòng lặp phòng ban.
//     private async Task<ForecastInputs> LoadInputsAsync(int year, int? departmentId)
//     {
//         List<(int Id, string Name)> departments;
//         if (departmentId.HasValue)
//         {
//             var department = await _repo.GetDepartmentAsync(departmentId.Value)
//                 ?? throw new ArgumentException($"Phòng ban (DepartmentId={departmentId}) không tồn tại.");
//             departments = new List<(int Id, string Name)> { department };
//         }
//         else
//         {
//             departments = await _repo.GetAllDepartmentsAsync();
//         }
//
//         // Ứng viên từ module Vòng đời; CHỈ giữ DueYear == năm dự báo để không cộng dồn giữa các năm (D6).
//         var all = await _lifecycle.GetReplacementCandidatesAsync(departmentId, year);
//
//         return new ForecastInputs(
//             year,
//             departments,
//             all.Where(c => c.DueYear == year).ToLookup(c => c.DepartmentId),
//             all.Where(c => c.DueYear < year).GroupBy(c => c.DepartmentId).ToDictionary(g => g.Key, g => g.Count()),
//             (await _repo.GetAllPricesAsync()).ToDictionary(p => p.CategoryId, p => p.UnitPrice),
//             (await _repo.GetAllCategoriesAsync()).ToDictionary(c => c.Id, c => c.Name),
//             (await _repo.GetPlansAsync(year, departmentId)).ToDictionary(p => p.DepartmentId),
//             await _repo.GetAllRatiosAsync(),
//             await _repo.GetActiveEmployeeCountsAsync(),
//             await _repo.GetUsableAssetCountsAsync(),
//             await _repo.GetAssetsWithoutPurchaseDateCountsAsync());
//     }
//
//     private sealed record RowComputation(
//         string DepartmentName,
//         List<ForecastBreakdownItemDto> Breakdown,
//         List<ExpansionBreakdownItemDto> Expansion,
//         ExpansionBreakdownEnvelope Envelope,
//         string InputsJson,
//         string InputsHash,
//         List<string> Warnings);
//
//     // Ảnh chụp đầu vào của một dòng dự báo — sắp xếp cố định để cùng đầu vào cho đúng cùng chuỗi JSON (và cùng hash).
//     private sealed class InputsSnapshot
//     {
//         public int Formula { get; set; } = FormulaVersion;
//         public int Baseline { get; set; }
//         public int Additional { get; set; }
//         public SortedDictionary<int, decimal> Ratios { get; set; } = new();
//         public SortedDictionary<int, decimal> Prices { get; set; } = new();
//         public SortedDictionary<int, int> Supply { get; set; } = new();
//         public List<int> Assets { get; set; } = new();
//     }
//
//     private RowComputation ComputeRow(ForecastInputs inputs, int departmentId, string departmentName)
//     {
//         var warnings = new List<string>();
//         var candidates = inputs.DueByDepartment[departmentId].ToList();
//
//         var breakdown = candidates
//             .GroupBy(c => (c.CategoryId, c.CategoryName))
//             .Select(g =>
//             {
//                 var count = g.Count();
//                 var hasPrice = inputs.Prices.TryGetValue(g.Key.CategoryId, out var unitPrice);
//                 return new ForecastBreakdownItemDto
//                 {
//                     CategoryId = g.Key.CategoryId,
//                     CategoryName = g.Key.CategoryName,
//                     AssetCount = count,
//                     UnitPrice = hasPrice ? unitPrice : null,
//                     Subtotal = hasPrice ? count * unitPrice : null,
//                     Assets = g.OrderBy(c => c.AssetCode, StringComparer.OrdinalIgnoreCase).Select(c => new ForecastAssetItemDto
//                     {
//                         AssetId = c.AssetId,
//                         AssetCode = c.AssetCode,
//                         AssetName = c.AssetName,
//                         PurchaseDate = c.PurchaseDate,
//                         AgeYears = c.AgeYears,
//                         TicketCount = c.TicketCount,
//                         MaxAgeYears = c.MaxAgeYears,
//                         MaxFailureCount = c.MaxFailureCount,
//                         Reasons = c.Reasons.ToList(),
//                         AgeDueDate = c.PurchaseDate?.AddYears(c.MaxAgeYears),
//                         Basis = BuildBasis(c),
//                     }).ToList(),
//                 };
//             })
//             .OrderBy(b => b.CategoryName, StringComparer.OrdinalIgnoreCase)
//             .ToList();
//
//         // ---- Mua thêm = phần THIẾU HỤT sau khi xét tồn kho dùng được ----
//         var baseline = inputs.Baseline.GetValueOrDefault(departmentId);                    // nhân viên hiện tại của phòng ban
//         var additional = inputs.Plans.TryGetValue(departmentId, out var plan) ? plan.AdditionalHeadcount : 0;   // E6: không có kế hoạch → 0
//         var rawProjected = baseline + additional;
//         var projected = Math.Max(0, rawProjected);
//         if (rawProjected < 0)
//             warnings.Add($"Phòng ban '{departmentName}': nhân viên hiện tại ({baseline}) + thay đổi kế hoạch ({additional}) < 0 — nhu cầu tính theo 0 người.");
//
//         var expansion = new List<ExpansionBreakdownItemDto>();
//         foreach (var ratio in inputs.Ratios)
//         {
//             // ⌈người × định mức⌉ bằng decimal (int × decimal → decimal; Ceiling trên decimal; tuyệt đối không qua double).
//             var demand = (int)Math.Ceiling(projected * ratio.DevicesPerPerson);
//             var usable = inputs.Supply.GetValueOrDefault((departmentId, ratio.CategoryId));
//             var due = candidates.Count(c => c.CategoryId == ratio.CategoryId);
//             var quantity = Math.Max(0, demand - usable);
//             var surplus = Math.Max(0, usable - demand);
//             if (demand == 0 && usable == 0) continue;
//
//             var name = inputs.CategoryNames.GetValueOrDefault(ratio.CategoryId) ?? $"Loại #{ratio.CategoryId}";
//             var hasPrice = inputs.Prices.TryGetValue(ratio.CategoryId, out var unitPrice);
//             var avoidable = Math.Min(due, surplus);
//
//             expansion.Add(new ExpansionBreakdownItemDto
//             {
//                 CategoryId = ratio.CategoryId,
//                 CategoryName = name,
//                 DevicesPerPerson = ratio.DevicesPerPerson,
//                 Demand = demand,
//                 ExistingUsable = usable,
//                 ReplacementDue = due,
//                 Surplus = surplus,
//                 ReplacementPossiblyAvoidable = avoidable,
//                 Quantity = quantity,
//                 UnitPrice = hasPrice ? unitPrice : null,
//                 Subtotal = hasPrice ? quantity * unitPrice : null,
//                 Explanation = BuildExplanation(projected, ratio.DevicesPerPerson, demand, usable, quantity, due),
//             });
//
//             if (avoidable > 0)
//                 warnings.Add($"Phòng ban '{departmentName}', loại '{name}': đang dư {surplus} thiết bị so với nhu cầu {demand}, có thể không cần mua thay {avoidable} thiết bị đến hạn " +
//                              "(dự báo vẫn tính đủ phần thay thế — hãy quyết định thủ công).");
//         }
//
//         expansion = expansion.OrderBy(i => i.CategoryName, StringComparer.Ordinal).ToList();
//
//         if (inputs.NoPurchaseDate.TryGetValue(departmentId, out var noDate) && noDate > 0)
//             warnings.Add($"Phòng ban '{departmentName}': {noDate} tài sản chưa có ngày mua nên không được xét tuổi thay thế (chỉ xét số lần lỗi) — có thể thiếu trong dự báo.");
//         if (inputs.Year > VietnamTime.Today().Year && inputs.CarryOverByDepartment.TryGetValue(departmentId, out var carry) && carry > 0)
//             warnings.Add($"Phòng ban '{departmentName}': {carry} tài sản đã tới hạn thay thế trước năm {inputs.Year} nên thuộc dự báo năm hiện tại, KHÔNG nằm trong dự báo năm {inputs.Year}.");
//
//         // ---- Ảnh chụp đầu vào ----
//         var relevantCategories = candidates.Select(c => c.CategoryId).Concat(inputs.Ratios.Select(r => r.CategoryId)).ToHashSet();
//         var snapshot = new InputsSnapshot
//         {
//             Baseline = baseline,
//             Additional = additional,
//             Ratios = new SortedDictionary<int, decimal>(inputs.Ratios.ToDictionary(r => r.CategoryId, r => r.DevicesPerPerson)),
//             Prices = new SortedDictionary<int, decimal>(inputs.Prices.Where(p => relevantCategories.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value)),
//             Supply = new SortedDictionary<int, int>(inputs.Ratios.ToDictionary(r => r.CategoryId, r => inputs.Supply.GetValueOrDefault((departmentId, r.CategoryId)))),
//             Assets = candidates.Select(c => c.AssetId).OrderBy(id => id).ToList(),
//         };
//         var json = JsonSerializer.Serialize(snapshot, JsonOptions);
//         var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
//
//         return new RowComputation(
//             departmentName,
//             breakdown,
//             expansion,
//             new ExpansionBreakdownEnvelope { BaselineHeadcount = baseline, AdditionalHeadcount = additional, ProjectedHeadcount = projected, Items = expansion },
//             json,
//             hash,
//             warnings);
//     }
//
//     // Căn cứ của từng tài sản thay thế: dùng đúng dữ liệu hợp đồng của module Vòng đời (ngưỡng, ngày mua, số phiếu) — không tự tính lại tuổi.
//     private static string BuildBasis(AssetReplacementCandidateDto c)
//     {
//         var parts = new List<string>();
//         if (c.Reasons.Contains("AgeExceeded"))
//             parts.Add($"đã quá tuổi (ngày mua {c.PurchaseDate:dd/MM/yyyy}, ngưỡng {c.MaxAgeYears} năm)");
//         if (c.Reasons.Contains("FailureCountExceeded"))
//             parts.Add($"vượt số phiếu bảo trì ({c.TicketCount} ≥ ngưỡng {c.MaxFailureCount})");
//         if (parts.Count == 0 && c.PurchaseDate.HasValue)
//             parts.Add($"đủ tuổi thay thế ngày {c.PurchaseDate.Value.AddYears(c.MaxAgeYears):dd/MM/yyyy} (mua {c.PurchaseDate:dd/MM/yyyy} + {c.MaxAgeYears} năm)");
//         if (parts.Count == 0)
//             parts.Add("thuộc diện thay thế theo module Vòng đời");
//         return string.Join("; ", parts);
//     }
//
//     private static string BuildExplanation(int projected, decimal ratio, int demand, int usable, int quantity, int due)
//     {
//         var text = $"Nhu cầu {demand} = ⌈{projected} người × {ratio:0.##}⌉; hiện có {usable} thiết bị dùng được";
//         text += quantity > 0 ? $" → thiếu {quantity} cần mua thêm." : " → đủ, không cần mua thêm.";
//         if (due > 0)
//             text += $" ({due} thiết bị đến hạn được thay 1:1 ở phần thay thế nên không làm giảm năng lực và không tính vào mua thêm.)";
//         return text;
//     }
//
//     // ---------------------------------------------------------------- đọc & kiểm tra "đã cũ"
//
//     public async Task<PagedResultDto<BudgetForecastResponseDto>> GetPagedAsync(
//         int? year, int? departmentId, int page, int pageSize,
//         string? keyword = null, string? kind = null, decimal? minBudget = null, decimal? maxBudget = null, string? sort = null)
//     {
//         (page, pageSize) = Paging.Normalize(page, pageSize);
//
//         if (kind is not null && kind is not ("replacement" or "expansion"))
//         {
//             throw new ArgumentException("kind không hợp lệ. Phải là một trong: replacement, expansion");
//         }
//
//         if (minBudget is < 0 || maxBudget is < 0)
//         {
//             throw new ArgumentException("Ngân sách không được âm.");
//         }
//
//         if (minBudget.HasValue && maxBudget.HasValue && minBudget > maxBudget)
//         {
//             throw new ArgumentException("minBudget không được lớn hơn maxBudget.");
//         }
//
//         if (sort is not null && sort is not ("year" or "budget" or "department" or "generated"))
//         {
//             throw new ArgumentException("sort không hợp lệ. Phải là một trong: year, budget, department, generated");
//         }
//
//         var (items, total) = await _repo.GetPagedAsync(year, departmentId, page, pageSize,
//             new ForecastListExtras { Keyword = keyword, Kind = kind, MinBudget = minBudget, MaxBudget = maxBudget, Sort = sort });
//
//         var responses = items.Select(f => ToResponse(f, f.Department.Name, ParseBreakdown(f))).ToList();
//         await ApplyStalenessAsync(items, responses);
//
//         return new PagedResultDto<BudgetForecastResponseDto>
//         {
//             Items = responses,
//             Page = page,
//             PageSize = pageSize,
//             TotalItems = total,
//         };
//     }
//
//     public async Task<BudgetForecastResponseDto> GetByIdAsync(int id)
//     {
//         var forecast = await _repo.GetByIdWithDetailsAsync(id)
//             ?? throw new BudgetForecastNotFoundException(id);
//         var response = ToResponse(forecast, forecast.Department.Name, ParseBreakdown(forecast));
//         await ApplyStalenessAsync(new[] { forecast }, new[] { response });
//         return response;
//     }
//
//     // So đầu vào HIỆN TẠI với ảnh chụp lúc tạo (cùng hàm ComputeRow nên không thể lệch quy tắc). Số liệu đã lưu KHÔNG bị đổi;
//     // chỉ gắn nhãn và liệt kê lý do để người dùng biết dự báo cũ vì sao khác với kết quả nếu tạo lại hôm nay.
//     // Mỗi năm xuất hiện trong trang chỉ nạp đầu vào một lần.
//     private async Task ApplyStalenessAsync(IReadOnlyList<BudgetForecast> forecasts, IReadOnlyList<BudgetForecastResponseDto> responses)
//     {
//         var inputsByYear = new Dictionary<int, ForecastInputs>();
//         for (var i = 0; i < forecasts.Count; i++)
//         {
//             var forecast = forecasts[i];
//             var response = responses[i];
//             if (string.IsNullOrEmpty(forecast.InputsJson))
//             {
//                 response.StaleStatus = "Unknown";
//                 response.StaleReasons = new List<string> { "Dự báo tạo trước khi hệ thống lưu dữ liệu đầu vào — hãy tạo lại để có cơ sở so sánh." };
//                 continue;
//             }
//
//             if (!inputsByYear.TryGetValue(forecast.Year, out var inputs))
//             {
//                 inputs = await LoadInputsAsync(forecast.Year, null);
//                 inputsByYear[forecast.Year] = inputs;
//             }
//
//             var department = inputs.Departments.FirstOrDefault(d => d.Id == forecast.DepartmentId);
//             if (department == default)
//             {
//                 response.StaleStatus = "Unknown";
//                 continue;
//             }
//
//             var current = ComputeRow(inputs, department.Id, department.Name);
//             if (current.InputsHash == forecast.InputsHash)
//             {
//                 response.StaleStatus = "Current";
//                 continue;
//             }
//
//             response.StaleStatus = "Stale";
//             response.StaleReasons = ExplainDifferences(forecast.InputsJson, current.InputsJson, inputs.CategoryNames);
//         }
//     }
//
//     private List<string> ExplainDifferences(string oldJson, string newJson, IReadOnlyDictionary<int, string> names)
//     {
//         InputsSnapshot? old;
//         InputsSnapshot? now;
//         try
//         {
//             old = JsonSerializer.Deserialize<InputsSnapshot>(oldJson, JsonOptions);
//             now = JsonSerializer.Deserialize<InputsSnapshot>(newJson, JsonOptions);
//         }
//         catch (JsonException)
//         {
//             return new List<string> { "Dữ liệu đầu vào đã lưu không đọc được — hãy tạo lại dự báo." };
//         }
//
//         if (old is null || now is null) return new List<string> { "Đầu vào đã thay đổi." };
//
//         string Name(int id) => names.TryGetValue(id, out var n) ? n : $"Loại #{id}";
//         var reasons = new List<string>();
//         if (old.Formula != now.Formula) reasons.Add("Công thức dự báo đã được cập nhật.");
//         if (old.Baseline != now.Baseline) reasons.Add($"Nhân viên hiện tại của phòng ban: {old.Baseline} → {now.Baseline}.");
//         if (old.Additional != now.Additional) reasons.Add($"Thay đổi nhân sự theo kế hoạch: {old.Additional} → {now.Additional}.");
//
//         void Diff<T>(string label, IDictionary<int, T> a, IDictionary<int, T> b, Func<T, string> format) where T : IEquatable<T>
//         {
//             foreach (var id in a.Keys.Union(b.Keys).OrderBy(k => k))
//             {
//                 var had = a.TryGetValue(id, out var x);
//                 var has = b.TryGetValue(id, out var y);
//                 if (had && has && x!.Equals(y)) continue;
//                 reasons.Add($"{label} '{Name(id)}': {(had ? format(x!) : "chưa có")} → {(has ? format(y!) : "đã xoá")}.");
//             }
//         }
//
//         Diff("Định mức", old.Ratios, now.Ratios, v => v.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")));
//         Diff("Đơn giá", old.Prices, now.Prices, v => v.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫");
//         Diff("Tồn kho dùng được", old.Supply, now.Supply, v => v.ToString());
//
//         var added = now.Assets.Except(old.Assets).Count();
//         var removed = old.Assets.Except(now.Assets).Count();
//         if (added > 0) reasons.Add($"{added} tài sản mới đến hạn thay thế trong năm.");
//         if (removed > 0) reasons.Add($"{removed} tài sản không còn thuộc diện thay thế trong năm (đã thanh lý, sửa ngày mua hoặc đổi ngưỡng).");
//
//         return reasons.Count > 0 ? reasons : new List<string> { "Đầu vào đã thay đổi." };
//     }
//
//     // Xoá mềm: chỉ đặt cờ, dòng vẫn nằm trong DB.
//     public async Task DeleteAsync(int id)
//     {
//         var forecast = await _repo.GetTrackedByIdAsync(id)
//             ?? throw new BudgetForecastNotFoundException(id);
//
//         forecast.IsDeleted = true;
//         forecast.DeletedAt = DateTime.UtcNow;
//         await _repo.SaveChangesAsync();
//     }
//
//     // ---------------------------------------------------------------- đơn giá tham khảo
//
//     public async Task<List<ReferencePriceDto>> GetPricesAsync()
//     {
//         var categories = await _repo.GetAllCategoriesAsync();
//         var prices = (await _repo.GetAllPricesAsync()).ToDictionary(p => p.CategoryId);
//         var editors = await _repo.GetUserNamesAsync(prices.Values.Where(p => p.UpdatedByUserId.HasValue).Select(p => p.UpdatedByUserId!.Value));
//
//         return categories.Select(c =>
//         {
//             prices.TryGetValue(c.Id, out var price);
//             return ToPriceDto(c.Id, c.Name, price, editors);
//         }).ToList();
//     }
//
//     public async Task<ReferencePriceDto> UpsertPriceAsync(int categoryId, UpsertReferencePriceRequestDto dto, int? actorUserId = null)
//     {
//         // Service tự kiểm tra lại vì có thể được gọi từ nơi khác ngoài Controller.
//         if (dto.UnitPrice <= 0 || dto.UnitPrice > UpsertReferencePriceRequestValidator.MaxUnitPrice || decimal.Round(dto.UnitPrice, 2) != dto.UnitPrice)
//             throw new ArgumentException("Đơn giá phải lớn hơn 0, tối đa 2 chữ số thập phân và không vượt quá giới hạn cho phép.");
//
//         var categories = await _repo.GetAllCategoriesAsync();
//         var category = categories.FirstOrDefault(c => c.Id == categoryId);
//         if (category == default)
//             throw new AssetCategoryNotFoundException(categoryId);
//
//         var price = await _repo.GetPriceAsync(categoryId);   // gồm cả dòng đã xoá mềm
//         var exists = price is not null && !price.IsDeleted;
//         ForecastInputGuard.EnsureVersion(exists, price?.Version, dto.Version, $"Đơn giá của loại '{category.Name}'");
//
//         if (price is null)
//         {
//             price = new AssetCategoryReferencePrice { CategoryId = categoryId };
//             await _repo.AddPriceAsync(price);
//         }
//         else if (exists && price.UnitPrice == dto.UnitPrice)
//         {
//             return ToPriceDto(categoryId, category.Name, price, await EditorNamesAsync(price.UpdatedByUserId));
//         }
//         else
//         {
//             price.IsDeleted = false;      // nhập lại đơn giá cho loại từng bị xoá mềm = khôi phục
//             price.DeletedAt = null;
//             _repo.UpdatePrice(price);
//         }
//
//         price.UnitPrice = dto.UnitPrice;
//         price.UpdatedAt = ForecastInputGuard.NowUtc();
//         price.UpdatedByUserId = actorUserId;
//
//         try
//         {
//             await _repo.SaveChangesAsync();
//         }
//         catch (DbUpdateConcurrencyException)
//         {
//             throw new ForecastInputConflictException($"Đơn giá của loại '{category.Name}' vừa được người khác thay đổi trong lúc bạn lưu. Vui lòng tải lại trang.");
//         }
//         catch (DbUpdateException)
//         {
//             throw new ForecastInputConflictException($"Loại '{category.Name}' vừa được người khác tạo đơn giá. Vui lòng tải lại trang.");
//         }
//
//         return ToPriceDto(categoryId, category.Name, price, await EditorNamesAsync(actorUserId));
//     }
//
//     public async Task DeletePriceAsync(int categoryId, int? version = null)
//     {
//         ForecastInputGuard.RequireVersionForDelete(version);
//         var price = await _repo.GetPriceAsync(categoryId);
//         if (price is null || price.IsDeleted)
//             throw new ReferencePriceNotFoundException(categoryId);
//         ForecastInputGuard.EnsureVersion(true, price.Version, version, "Đơn giá");
//
//         // Xoá mềm: giữ dòng trong DB, chỉ ẩn khỏi tính toán và giao diện.
//         price.IsDeleted = true;
//         price.DeletedAt = DateTime.UtcNow;
//         _repo.UpdatePrice(price);
//         try
//         {
//             await _repo.SaveChangesAsync();
//         }
//         catch (DbUpdateConcurrencyException)
//         {
//             throw new ForecastInputConflictException("Đơn giá vừa được người khác thay đổi hoặc đã bị xoá. Vui lòng tải lại trang.");
//         }
//     }
//
//     private async Task<Dictionary<int, string>> EditorNamesAsync(int? userId) =>
//         userId.HasValue ? await _repo.GetUserNamesAsync(new[] { userId.Value }) : new Dictionary<int, string>();
//
//     private static ReferencePriceDto ToPriceDto(int categoryId, string name, AssetCategoryReferencePrice? price, IReadOnlyDictionary<int, string> editors) => new()
//     {
//         CategoryId = categoryId,
//         CategoryName = name,
//         UnitPrice = price?.UnitPrice,
//         Version = price?.Version,
//         UpdatedAt = ForecastInputGuard.AsUtc(price?.UpdatedAt),
//         UpdatedByName = price?.UpdatedByUserId is { } id ? (editors.TryGetValue(id, out var n) ? n : "(tài khoản không còn)") : null,
//     };
//
//     // ---------------------------------------------------------------- nội bộ
//
//     // "Thiếu đơn giá: Máy in (3 thiết bị); Switch (1 thiết bị)" hoặc null; cắt cho vừa cột Notes để tránh DbUpdateException.
//     internal static string? BuildMissingPriceNote(
//         IEnumerable<ForecastBreakdownItemDto> breakdown, IEnumerable<ExpansionBreakdownItemDto>? expansion = null)
//     {
//         // Gộp thay thế + mua thêm theo loại để mỗi loại thiếu giá chỉ xuất hiện một lần trong ghi chú.
//         var missing = breakdown.Where(b => b.UnitPrice is null).Select(b => (b.CategoryName, Count: b.AssetCount))
//             .Concat((expansion ?? Enumerable.Empty<ExpansionBreakdownItemDto>())
//                 .Where(i => i.UnitPrice is null && i.Quantity > 0).Select(i => (i.CategoryName, Count: i.Quantity)))
//             .GroupBy(m => m.CategoryName)
//             .Select(g => (CategoryName: g.Key, Count: g.Sum(m => m.Count)))
//             .OrderBy(m => m.CategoryName, StringComparer.OrdinalIgnoreCase)
//             .ToList();
//         if (missing.Count == 0) return null;
//
//         var text = "Thiếu đơn giá: " + string.Join("; ", missing.Select(b => $"{b.CategoryName} ({b.Count} thiết bị)"));
//         return text.Length <= NotesMaxLength ? text : text[..(NotesMaxLength - 1)] + "…";
//     }
//
//     private List<ForecastBreakdownItemDto> ParseBreakdown(BudgetForecast forecast)
//     {
//         if (string.IsNullOrWhiteSpace(forecast.BreakdownJson)) return new List<ForecastBreakdownItemDto>();
//
//         try
//         {
//             return JsonSerializer.Deserialize<List<ForecastBreakdownItemDto>>(forecast.BreakdownJson, JsonOptions)
//                    ?? new List<ForecastBreakdownItemDto>();
//         }
//         catch (JsonException ex)
//         {
//             // JSON hỏng không được làm hỏng cả trang lịch sử: trả danh sách rỗng và ghi cảnh báo.
//             _logger.LogWarning(ex, "BreakdownJson của dự báo Id={ForecastId} không đọc được.", forecast.Id);
//             return new List<ForecastBreakdownItemDto>();
//         }
//     }
//
//     // Dòng cũ (tạo trước Tuần 8) có ExpansionBreakdownJson = null → mua thêm 0; JSON hỏng → rỗng + cảnh báo, không ném.
//     private ExpansionBreakdownEnvelope ParseExpansion(BudgetForecast forecast)
//     {
//         if (string.IsNullOrWhiteSpace(forecast.ExpansionBreakdownJson)) return new ExpansionBreakdownEnvelope();
//
//         try
//         {
//             return JsonSerializer.Deserialize<ExpansionBreakdownEnvelope>(forecast.ExpansionBreakdownJson, JsonOptions)
//                    ?? new ExpansionBreakdownEnvelope();
//         }
//         catch (JsonException ex)
//         {
//             _logger.LogWarning(ex, "ExpansionBreakdownJson của dự báo Id={ForecastId} không đọc được.", forecast.Id);
//             return new ExpansionBreakdownEnvelope();
//         }
//     }
//
//     private BudgetForecastResponseDto ToResponse(BudgetForecast f, string departmentName, List<ForecastBreakdownItemDto> breakdown) =>
//         ToResponse(f, departmentName, breakdown, ParseExpansion(f));
//
//     private static BudgetForecastResponseDto ToResponse(
//         BudgetForecast f, string departmentName, List<ForecastBreakdownItemDto> breakdown, ExpansionBreakdownEnvelope envelope)
//     {
//         var expansion = envelope.Items ?? new List<ExpansionBreakdownItemDto>();
//         return new BudgetForecastResponseDto
//         {
//             Id = f.Id,
//             Year = f.Year,
//             DepartmentId = f.DepartmentId,
//             DepartmentName = departmentName,
//             EstimatedReplacementCount = f.EstimatedReplacementCount,
//             EstimatedBudget = f.EstimatedBudget,
//             Notes = f.Notes,
//             GeneratedAt = ForecastInputGuard.AsUtc(f.GeneratedAt),
//             Breakdown = breakdown,
//             AdditionalHeadcount = envelope.AdditionalHeadcount,
//             BaselineHeadcount = envelope.BaselineHeadcount,
//             ProjectedHeadcount = envelope.ProjectedHeadcount,
//             EstimatedExpansionCount = f.EstimatedExpansionCount,
//             EstimatedExpansionBudget = f.EstimatedExpansionBudget,
//             ExpansionBreakdown = expansion,
//             TotalPurchaseCount = f.EstimatedReplacementCount + f.EstimatedExpansionCount,
//             TotalBudget = f.EstimatedBudget + f.EstimatedExpansionBudget,
//             PurchaseSummary = BuildPurchaseSummary(breakdown, expansion),
//             Revision = f.Revision,
//         };
//     }
//
//     // Danh sách mua sắm: hợp nhất thay thế + mua thêm theo CategoryId; sắp theo tên loại bằng Ordinal (không phụ thuộc culture).
//     // Loại chỉ xuất hiện ở phần mua thêm với số lượng 0 (đủ tồn kho) không đưa vào danh sách mua sắm.
//     public static List<PurchaseSummaryItemDto> BuildPurchaseSummary(
//         IEnumerable<ForecastBreakdownItemDto> breakdown, IEnumerable<ExpansionBreakdownItemDto> expansion)
//     {
//         var byCategory = new Dictionary<int, PurchaseSummaryItemDto>();
//
//         foreach (var b in breakdown)
//         {
//             byCategory[b.CategoryId] = new PurchaseSummaryItemDto
//             {
//                 CategoryId = b.CategoryId,
//                 CategoryName = b.CategoryName,
//                 ReplacementQuantity = b.AssetCount,
//                 UnitPrice = b.UnitPrice,
//             };
//         }
//
//         foreach (var e in expansion.Where(e => e.Quantity > 0))
//         {
//             if (!byCategory.TryGetValue(e.CategoryId, out var item))
//             {
//                 item = new PurchaseSummaryItemDto { CategoryId = e.CategoryId, CategoryName = e.CategoryName };
//                 byCategory[e.CategoryId] = item;
//             }
//             item.ExpansionQuantity = e.Quantity;
//             if (e.UnitPrice is not null) item.UnitPrice = e.UnitPrice;   // lệch do dữ liệu cũ → ưu tiên mục mua thêm
//         }
//
//         foreach (var item in byCategory.Values)
//         {
//             item.TotalQuantity = item.ReplacementQuantity + item.ExpansionQuantity;
//             item.TotalCost = item.UnitPrice.HasValue ? item.TotalQuantity * item.UnitPrice.Value : null;
//         }
//
//         return byCategory.Values.OrderBy(i => i.CategoryName, StringComparer.Ordinal).ToList();
//     }
//
//     // Định dạng ExpansionBreakdownJson: MỘT object, không phải mảng trơn. Dòng cũ thiếu Baseline/Projected → 0.
//     private sealed class ExpansionBreakdownEnvelope
//     {
//         public int BaselineHeadcount { get; set; }
//         public int AdditionalHeadcount { get; set; }
//         public int ProjectedHeadcount { get; set; }
//         public List<ExpansionBreakdownItemDto>? Items { get; set; } = new();
//     }
// }
