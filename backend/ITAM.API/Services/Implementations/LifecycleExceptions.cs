namespace ITAM.API.Services.Implementations;

public class LifecyclePolicyNotFoundException : Exception
{
    public LifecyclePolicyNotFoundException(int categoryId)
        : base($"Loại tài sản (CategoryId={categoryId}) chưa có ngưỡng riêng để xoá.")
    {
    }
}

// Tài sản không tồn tại hoặc đã thanh lý (Disposed không tham gia phân tích vòng đời).
public class LifecycleAssetNotFoundException : Exception
{
    public LifecycleAssetNotFoundException(int assetId)
        : base($"Không tìm thấy tài sản (AssetId={assetId}) trong phân tích vòng đời (không tồn tại hoặc đã thanh lý).")
    {
    }
}
