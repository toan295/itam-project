namespace ITAM.API.Services.Implementations;

public class AllocationNotFoundException : Exception
{
    public AllocationNotFoundException(int id)
        : base($"Không tìm thấy phân bổ tài sản Id={id}.")
    {
    }
}

public class AssetNotAvailableForAllocationException : Exception
{
    public AssetNotAvailableForAllocationException(int assetId)
        : base($"Tài sản Id={assetId} không ở trạng thái InUse nên không thể phân bổ.")
    {
    }
}

public class AssetAlreadyAllocatedException : Exception
{
    public AssetAlreadyAllocatedException(int assetId)
        : base($"Tài sản Id={assetId} đang có một phân bổ chưa thu hồi.")
    {
    }
}

public class AllocationAlreadyReturnedException : Exception
{
    public AllocationAlreadyReturnedException(int id)
        : base($"Phân bổ Id={id} đã được thu hồi trước đó.")
    {
    }
}
