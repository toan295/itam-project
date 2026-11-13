// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using ITAM.API.Models.DTOs.Common;
// using ITAM.API.Models.DTOs.Forecasts;
//
// namespace ITAM.API.Services.Interfaces;
//
// public interface IBudgetForecastService
// {
//     // Tuần tự hoá theo năm (hai lần chạy cùng năm không chạy đồng thời); đầu vào đọc trong một transaction nhất quán;
//     // chạy lại với đầu vào y hệt không ghi gì. Mọi dòng được lưu trong MỘT lần SaveChanges (tất cả hoặc không dòng nào).
//     Task<ForecastGenerationResultDto> GenerateAsync(GenerateForecastRequestDto dto);
//     Task<PagedResultDto<BudgetForecastResponseDto>> GetPagedAsync(
//         int? year, int? departmentId, int page, int pageSize,
//         string? keyword = null, string? kind = null, decimal? minBudget = null, decimal? maxBudget = null, string? sort = null);
//     Task<BudgetForecastResponseDto> GetByIdAsync(int id);
//     Task DeleteAsync(int id);
//
//     Task<List<ReferencePriceDto>> GetPricesAsync();
//     Task<ReferencePriceDto> UpsertPriceAsync(int categoryId, UpsertReferencePriceRequestDto dto, int? actorUserId = null);
//     Task DeletePriceAsync(int categoryId, int? version = null);
// }
