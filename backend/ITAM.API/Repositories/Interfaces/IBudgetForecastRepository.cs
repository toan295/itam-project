using ITAM.API.Models.Entities;

namespace ITAM.API.Repositories.Interfaces;

public interface IBudgetForecastRepository
{
    Task<(List<BudgetForecast> Items, int TotalItems)> GetPagedAsync(int? year, int? departmentId, int page, int pageSize);
    Task<BudgetForecast?> GetByIdWithDetailsAsync(int id);
    Task<BudgetForecast?> GetByYearAndDepartmentAsync(int year, int departmentId);
    Task<BudgetForecast?> GetTrackedByIdAsync(int id);
    Task AddAsync(BudgetForecast forecast);
    void Update(BudgetForecast forecast);
    Task<int> SaveChangesAsync();

    // Đọc trực tiếp Departments/AssetCategories qua AppDbContext dùng chung (tiền lệ SoftwareLicenseRepository.AssetExistsAsync).
    Task<List<(int Id, string Name)>> GetAllDepartmentsAsync();
    Task<(int Id, string Name)?> GetDepartmentAsync(int id);

    // Đơn giá tham khảo
    Task<List<AssetCategoryReferencePrice>> GetAllPricesAsync();
    // Gồm cả đơn giá đã xoá mềm (kiểm tra IsDeleted).
    Task<AssetCategoryReferencePrice?> GetPriceAsync(int categoryId);
    Task AddPriceAsync(AssetCategoryReferencePrice price);
    void UpdatePrice(AssetCategoryReferencePrice price);
    Task<List<(int Id, string Name)>> GetAllCategoriesAsync();
    Task<bool> CategoryExistsAsync(int categoryId);

    // Kế hoạch mở rộng nhân sự (departmentId = null → mọi phòng ban). GetPlanAsync có tracking để Service sửa trực tiếp.
    Task<List<DepartmentExpansionPlan>> GetPlansAsync(int year, int? departmentId);
    Task<DepartmentExpansionPlan?> GetPlanAsync(int year, int departmentId);
    Task AddPlanAsync(DepartmentExpansionPlan plan);
    void RemovePlan(DepartmentExpansionPlan plan);

    // Định mức thiết bị/người theo loại tài sản
    Task<List<AssetCategoryDeviceRatio>> GetAllRatiosAsync();
    Task<AssetCategoryDeviceRatio?> GetRatioAsync(int categoryId);
    Task AddRatioAsync(AssetCategoryDeviceRatio ratio);
    void RemoveRatio(AssetCategoryDeviceRatio ratio);
}
