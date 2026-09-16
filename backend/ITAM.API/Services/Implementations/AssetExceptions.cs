namespace ITAM.API.Services.Implementations;

public class AssetCodeAlreadyExistsException : Exception
{
    public AssetCodeAlreadyExistsException(string assetCode)
        : base($"Mã tài sản '{assetCode}' đã tồn tại.")
    {
    }
}

public class AssetNotFoundException : Exception
{
    public AssetNotFoundException(int id)
        : base($"Không tìm thấy tài sản Id={id}.")
    {
    }
}

// UC-05 E3 / UC-06 E2: Manager chỉ được thêm/sửa tài sản trong phòng ban mình phụ trách.
public class DepartmentForbiddenException : Exception
{
    public DepartmentForbiddenException()
        : base("Bạn không có quyền thao tác trên tài sản của phòng ban khác.")
    {
    }
}

// UC-07: chỉ Admin IT được chuyển tài sản sang Disposed (qua DELETE /assets/{id}) — chặn Manager
// "lách" qua PUT /assets/{id} để tự đặt Status = Disposed.
public class AssetDisposalNotAllowedException : Exception
{
    public AssetDisposalNotAllowedException()
        : base("Chỉ Admin IT được ngừng sử dụng tài sản. Vui lòng dùng chức năng \"Thanh lý\" (DELETE).")
    {
    }
}
