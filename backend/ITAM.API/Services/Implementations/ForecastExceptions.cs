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

// Đầu vào dự báo (kế hoạch nhân sự, định mức, đơn giá) đã bị người khác tạo/sửa/xoá kể từ lúc client tải — 409.
public class ForecastInputConflictException : Exception
{
    public ForecastInputConflictException(string message) : base(message)
    {
    }
}

// Đang có lần tạo dự báo khác cho cùng năm (khoá tuần tự hoá bị quá thời gian chờ) — 409, thử lại sau.
public class ForecastBusyException : Exception
{
    public ForecastBusyException(int year)
        : base($"Đang có lần tạo dự báo khác cho năm {year}. Vui lòng đợi vài giây rồi thử lại.")
    {
    }
}
