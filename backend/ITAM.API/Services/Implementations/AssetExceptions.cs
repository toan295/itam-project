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

// Không cho đặt Status = Disposed trực tiếp qua PUT /assets/{id} — thanh lý bắt buộc qua quy trình
// phiếu thanh lý (/disposal-requests) để có đủ kiểm tra, đề xuất và duyệt.
public class AssetDisposalNotAllowedException : Exception
{
    public AssetDisposalNotAllowedException()
        : base("Không thể đặt trạng thái \"Đã thanh lý\" trực tiếp. Hãy dùng quy trình Thanh lý: " +
               "Technician kiểm tra và đề xuất, Manager duyệt, Admin IT thực hiện.")
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

public class AssetSerialNumberAlreadyExistsException : Exception
{
    public AssetSerialNumberAlreadyExistsException(string serialNumber)
        : base($"Số serial '{serialNumber}' đã được dùng cho một tài sản khác.")
    {
    }
}
