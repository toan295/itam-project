namespace ITAM.API.Services.Implementations;

public class BudgetForecastNotFoundException : Exception
{
    public BudgetForecastNotFoundException(int id)
        : base($"Không tìm thấy dự báo ngân sách Id={id}.")
    {
    }
}

public class ReferencePriceNotFoundException : Exception
{
    public ReferencePriceNotFoundException(int categoryId)
        : base($"Loại tài sản Id={categoryId} chưa có đơn giá tham khảo để xoá.")
    {
    }
}

public class ExpansionPlanNotFoundException : Exception
{
    public ExpansionPlanNotFoundException(int year, int departmentId)
        : base($"Chưa có kế hoạch mở rộng năm {year} cho phòng ban Id={departmentId} để xoá.")
    {
    }
}

// Race condition: 2 request cùng tạo kế hoạch của một (năm, phòng ban) — unique index là chốt chặn cuối.
public class ExpansionPlanConflictException : Exception
{
    public ExpansionPlanConflictException(int year, int departmentId)
        : base($"Kế hoạch mở rộng năm {year} của phòng ban Id={departmentId} vừa được người khác tạo/sửa. Vui lòng tải lại và thử lại.")
    {
    }
}

public class DeviceRatioNotFoundException : Exception
{
    public DeviceRatioNotFoundException(int categoryId)
        : base($"Loại tài sản Id={categoryId} chưa có định mức thiết bị/người để xoá.")
    {
    }
}
