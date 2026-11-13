// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using ITAM.API.Authorization;
// using FluentValidation;
// using ITAM.API.Models.DTOs;
// using ITAM.API.Models.DTOs.Forecasts;
// using ITAM.API.Services.Implementations;
// using ITAM.API.Services.Interfaces;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using System.Security.Claims;
//
// namespace ITAM.API.Controllers;
//
// // UC-17: Dự báo thay thế & ngân sách — chỉ Admin IT (Mục 8.2: /api/v1/forecasts).
// [ApiController]
// [Route("api/v1/forecasts")]
// [HasPermission(Permissions.ForecastsManage)]
// public class BudgetForecastsController : ControllerBase
// {
//     private readonly IBudgetForecastService _service;
//     private readonly IValidator<GenerateForecastRequestDto> _generateValidator;
//     private readonly IValidator<UpsertReferencePriceRequestDto> _priceValidator;
//     private readonly IExpansionPlanningService _expansionService;
//     private readonly IValidator<UpsertExpansionPlanRequestDto> _planValidator;
//     private readonly IValidator<UpsertDeviceRatioRequestDto> _ratioValidator;
//
//     public BudgetForecastsController(
//         IBudgetForecastService service,
//         IValidator<GenerateForecastRequestDto> generateValidator,
//         IValidator<UpsertReferencePriceRequestDto> priceValidator,
//         IExpansionPlanningService expansionService,
//         IValidator<UpsertExpansionPlanRequestDto> planValidator,
//         IValidator<UpsertDeviceRatioRequestDto> ratioValidator)
//     {
//         _service = service;
//         _generateValidator = generateValidator;
//         _priceValidator = priceValidator;
//         _expansionService = expansionService;
//         _planValidator = planValidator;
//         _ratioValidator = ratioValidator;
//     }
//
//     // 200 (không phải 201): lần gọi có thể chỉ cập nhật dòng dự báo đã có của cùng (năm, phòng ban).
//     [HttpPost("generate")]
//     public async Task<IActionResult> Generate(GenerateForecastRequestDto dto)
//     {
//         var validation = await _generateValidator.ValidateAsync(dto);
//         if (!validation.IsValid)
//         {
//             return BadRequest(ApiResponse<object>.Fail(
//                 "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
//         }
//
//         try
//         {
//             var result = await _service.GenerateAsync(dto);
//             return Ok(ApiResponse<object>.Ok(result, "Đã tạo dự báo ngân sách."));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (Exception ex) when (ex is ForecastBusyException or ForecastInputConflictException)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpGet]
//     public async Task<IActionResult> GetList(
//         [FromQuery] int? year, [FromQuery] int? departmentId,
//         [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
//         [FromQuery] string? keyword = null, [FromQuery] string? kind = null,
//         [FromQuery] decimal? minBudget = null, [FromQuery] decimal? maxBudget = null, [FromQuery] string? sort = null)
//     {
//         try
//         {
//             var result = await _service.GetPagedAsync(year, departmentId, page, pageSize, keyword, kind, minBudget, maxBudget, sort);
//             return Ok(ApiResponse<object>.Ok(result));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     // {id:int}: route cố định "reference-prices" bên dưới không bị nhầm với {id}.
//     [HttpGet("{id:int}")]
//     public async Task<IActionResult> GetById(int id)
//     {
//         try
//         {
//             var result = await _service.GetByIdAsync(id);
//             return Ok(ApiResponse<object>.Ok(result));
//         }
//         catch (BudgetForecastNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     // Xoá mềm: dòng dự báo chỉ bị ẩn khỏi giao diện, vẫn còn trong DB.
//     [HttpDelete("{id:int}")]
//     public async Task<IActionResult> Delete(int id)
//     {
//         try
//         {
//             await _service.DeleteAsync(id);
//             return NoContent();
//         }
//         catch (BudgetForecastNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpGet("reference-prices")]
//     public async Task<IActionResult> GetReferencePrices()
//     {
//         var result = await _service.GetPricesAsync();
//         return Ok(ApiResponse<object>.Ok(result));
//     }
//
//     [HttpPut("reference-prices/{categoryId:int}")]
//     public async Task<IActionResult> UpsertReferencePrice(int categoryId, UpsertReferencePriceRequestDto dto)
//     {
//         var validation = await _priceValidator.ValidateAsync(dto);
//         if (!validation.IsValid)
//         {
//             return BadRequest(ApiResponse<object>.Fail(
//                 "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
//         }
//
//         try
//         {
//             var result = await _service.UpsertPriceAsync(categoryId, dto, GetCurrentUserId());
//             return Ok(ApiResponse<object>.Ok(result, "Đã lưu đơn giá tham khảo."));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (AssetCategoryNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ForecastInputConflictException ex)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpDelete("reference-prices/{categoryId:int}")]
//     public async Task<IActionResult> DeleteReferencePrice(int categoryId, [FromQuery] int? version = null)
//     {
//         try
//         {
//             await _service.DeletePriceAsync(categoryId, version);
//             return NoContent();
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ReferencePriceNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ForecastInputConflictException ex)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     // ---- Kế hoạch mở rộng nhân sự theo phòng ban (đầu vào dự báo mua thêm) ----
//
//     // year bắt buộc: thiếu năm → 0 → Service trả 400 "Năm phải từ ...".
//     [HttpGet("expansion-plans")]
//     public async Task<IActionResult> GetExpansionPlans([FromQuery] int year, [FromQuery] int? departmentId)
//     {
//         try
//         {
//             var result = await _expansionService.GetPlansAsync(year, departmentId);
//             return Ok(ApiResponse<object>.Ok(result));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (DepartmentNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpPut("expansion-plans/{year:int}/{departmentId:int}")]
//     public async Task<IActionResult> UpsertExpansionPlan(int year, int departmentId, UpsertExpansionPlanRequestDto dto)
//     {
//         var validation = await _planValidator.ValidateAsync(dto);
//         if (!validation.IsValid)
//         {
//             return BadRequest(ApiResponse<object>.Fail(
//                 "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
//         }
//
//         try
//         {
//             var result = await _expansionService.UpsertPlanAsync(year, departmentId, dto, GetCurrentUserId());
//             return Ok(ApiResponse<object>.Ok(result, "Đã lưu kế hoạch mở rộng."));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (DepartmentNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (Exception ex) when (ex is ExpansionPlanConflictException or ForecastInputConflictException)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpDelete("expansion-plans/{year:int}/{departmentId:int}")]
//     public async Task<IActionResult> DeleteExpansionPlan(int year, int departmentId, [FromQuery] int? version = null)
//     {
//         try
//         {
//             await _expansionService.DeletePlanAsync(year, departmentId, version);
//             return NoContent();
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ExpansionPlanNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ForecastInputConflictException ex)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     // ---- Định mức thiết bị/người theo loại tài sản ----
//
//     [HttpGet("device-ratios")]
//     public async Task<IActionResult> GetDeviceRatios()
//     {
//         var result = await _expansionService.GetRatiosAsync();
//         return Ok(ApiResponse<object>.Ok(result));
//     }
//
//     [HttpPut("device-ratios/{categoryId:int}")]
//     public async Task<IActionResult> UpsertDeviceRatio(int categoryId, UpsertDeviceRatioRequestDto dto)
//     {
//         var validation = await _ratioValidator.ValidateAsync(dto);
//         if (!validation.IsValid)
//         {
//             return BadRequest(ApiResponse<object>.Fail(
//                 "Dữ liệu không hợp lệ.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
//         }
//
//         try
//         {
//             var result = await _expansionService.UpsertRatioAsync(categoryId, dto, GetCurrentUserId());
//             return Ok(ApiResponse<object>.Ok(result, "Đã lưu định mức thiết bị/người."));
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (AssetCategoryNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ForecastInputConflictException ex)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     [HttpDelete("device-ratios/{categoryId:int}")]
//     public async Task<IActionResult> DeleteDeviceRatio(int categoryId, [FromQuery] int? version = null)
//     {
//         try
//         {
//             await _expansionService.DeleteRatioAsync(categoryId, version);
//             return NoContent();
//         }
//         catch (ArgumentException ex)
//         {
//             return BadRequest(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (DeviceRatioNotFoundException ex)
//         {
//             return NotFound(ApiResponse<object>.Fail(ex.Message));
//         }
//         catch (ForecastInputConflictException ex)
//         {
//             return Conflict(ApiResponse<object>.Fail(ex.Message));
//         }
//     }
//
//     private int? GetCurrentUserId() =>
//         int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
// }
