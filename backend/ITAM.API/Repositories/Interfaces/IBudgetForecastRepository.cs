// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này
// và của các chỗ đánh dấu [ĐÃ ĐÓNG] ở file khác, rồi tạo migration (dotnet ef migrations add ...) để tạo lại 4 bảng dự báo.
// using ITAM.API.Repositories.Models;
// using ITAM.API.Models.Entities;
//
// namespace ITAM.API.Repositories.Interfaces;
//
// public interface IBudgetForecastRepository
// {
//     Task<(List<BudgetForecast> Items, int TotalItems)> GetPagedAsync(
//         int? year, int? departmentId, int page, int pageSize, ForecastListExtras extras);
//     Task<BudgetForecast?> GetByIdWithDetailsAsync(int id);
//     // Mọi dòng dự báo của năm (có tracking, GỒM dòng đã xoá mềm vì unique index (Năm, Phòng ban) vẫn tính chúng) — nạp một lần cho cả lần chạy.
//     Task<List<BudgetForecast>> GetForecastsByYearAsync(int year);
//     Task<BudgetForecast?> GetTrackedByIdAsync(int id);
//     Task AddAsync(BudgetForecast forecast);
//     void Update(BudgetForecast forecast);
//     Task<int> SaveChangesAsync();
//
//     // Đọc trực tiếp Departments/AssetCategories qua AppDbContext dùng chung (tiền lệ SoftwareLicenseRepository.AssetExistsAsync).
//     Task<List<(int Id, string Name)>> GetAllDepartmentsAsync();
//     Task<(int Id, string Name)?> GetDepartmentAsync(int id);
//
//     // Đơn giá tham khảo
//     Task<List<AssetCategoryReferencePrice>> GetAllPricesAsync();
//     // Gồm cả đơn giá đã xoá mềm (kiểm tra IsDeleted).
//     Task<AssetCategoryReferencePrice?> GetPriceAsync(int categoryId);
//     Task AddPriceAsync(AssetCategoryReferencePrice price);
//     void UpdatePrice(AssetCategoryReferencePrice price);
//     Task<List<(int Id, string Name)>> GetAllCategoriesAsync();
//     Task<bool> CategoryExistsAsync(int categoryId);
//
//     // Dữ liệu nền cho mô hình mua thêm (đọc trực tiếp Employees/Assets qua AppDbContext, mỗi hàm đúng 1 truy vấn gom nhóm).
//     Task<Dictionary<int, int>> GetActiveEmployeeCountsAsync();                     // phòng ban -> số nhân viên đang hoạt động
//     Task<Dictionary<(int DepartmentId, int CategoryId), int>> GetUsableAssetCountsAsync();   // tài sản InUse/Maintenance theo (phòng ban, loại)
//     Task<Dictionary<int, int>> GetAssetsWithoutPurchaseDateCountsAsync();          // phòng ban -> số tài sản chưa thanh lý thiếu ngày mua
//     Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds);
//
//     // Kế hoạch mở rộng nhân sự (departmentId = null → mọi phòng ban). GetPlanAsync có tracking để Service sửa trực tiếp.
//     Task<List<DepartmentExpansionPlan>> GetPlansAsync(int year, int? departmentId);
//     Task<DepartmentExpansionPlan?> GetPlanAsync(int year, int departmentId);
//     Task AddPlanAsync(DepartmentExpansionPlan plan);
//     void RemovePlan(DepartmentExpansionPlan plan);
//
//     // Định mức thiết bị/người theo loại tài sản
//     Task<List<AssetCategoryDeviceRatio>> GetAllRatiosAsync();
//     Task<AssetCategoryDeviceRatio?> GetRatioAsync(int categoryId);
//     Task AddRatioAsync(AssetCategoryDeviceRatio ratio);
//     void RemoveRatio(AssetCategoryDeviceRatio ratio);
// }
