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

// UC-07 E1: không cho thanh lý tài sản khi còn AssetAllocation đang mở (chưa thu hồi) — phải thu hồi
// trước (UC-15). Tách riêng khỏi AssetDisposalNotAllowedException: lý do khác hẳn (409 - xung đột
// trạng thái, không phải 403 - thiếu quyền), dùng chung sẽ hiển thị nhầm thông báo "chỉ Admin IT..."
// cho một lỗi hoàn toàn không liên quan tới quyền hạn.
public class AssetHasOpenAllocationException : Exception
{
    public AssetHasOpenAllocationException(int assetId)
        : base($"Tài sản Id={assetId} đang có một phân bổ chưa thu hồi. Vui lòng thu hồi trước khi thanh lý.")
    {
    }
}

// Chiều ngược lại của UC-07: chỉ Admin IT được đưa tài sản đã thanh lý (Disposed) trở lại hoạt động.
public class AssetReactivationNotAllowedException : Exception
{
    public AssetReactivationNotAllowedException()
        : base("Tài sản đã thanh lý. Chỉ Admin IT được khôi phục trạng thái của tài sản này.")
    {
    }
}
