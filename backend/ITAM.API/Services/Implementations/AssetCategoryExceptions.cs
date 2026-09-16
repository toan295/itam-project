namespace ITAM.API.Services.Implementations;

public class AssetCategoryNameAlreadyExistsException : Exception
{
    public AssetCategoryNameAlreadyExistsException(string name)
        : base($"Loại tài sản '{name}' đã tồn tại.")
    {
    }
}

public class AssetCategoryNotFoundException : Exception
{
    public AssetCategoryNotFoundException(int id)
        : base($"Không tìm thấy loại tài sản Id={id}.")
    {
    }
}

// UC-04 E1: không xoá danh mục đang được tài sản tham chiếu.
public class AssetCategoryInUseException : Exception
{
    public AssetCategoryInUseException(int id)
        : base($"Không thể xoá loại tài sản Id={id} vì vẫn còn tài sản đang sử dụng loại này.")
    {
    }
}
