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
