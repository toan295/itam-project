namespace ITAM.API.Repositories.Models;

// Bộ lọc bổ sung cho danh sách dự báo ngân sách (Service đã validate).
// Kind: replacement (có thiết bị thay thế) | expansion (có thiết bị mua thêm). MinBudget/MaxBudget so với TỔNG ngân sách (thay thế + mua thêm).
// Sort: year (mặc định: năm mới nhất, rồi theo phòng ban) | budget (tổng ngân sách giảm dần) | department | generated (mới tạo/cập nhật nhất).
public class ForecastListExtras
{
    public string? Keyword { get; set; }
    public string? Kind { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public string? Sort { get; set; }
}
