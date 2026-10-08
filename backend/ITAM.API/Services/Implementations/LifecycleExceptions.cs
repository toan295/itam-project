namespace ITAM.API.Services.Implementations;

public class LifecyclePolicyNotFoundException : Exception
{
    public LifecyclePolicyNotFoundException(int categoryId)
        : base($"Loại tài sản (CategoryId={categoryId}) chưa có ngưỡng riêng để xoá.")
    {
    }
}
