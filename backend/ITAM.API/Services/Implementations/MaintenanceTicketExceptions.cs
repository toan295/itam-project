using ITAM.API.Models.Enums;

namespace ITAM.API.Services.Implementations;

public class MaintenanceTicketNotFoundException : Exception
{
    public MaintenanceTicketNotFoundException(int id)
        : base($"Không tìm thấy phiếu bảo trì Id={id}.")
    {
    }
}

// UC-11 điều kiện trước: chỉ tạo phiếu cho tài sản đang InUse hoặc Broken.
public class AssetNotEligibleForMaintenanceException : Exception
{
    public AssetNotEligibleForMaintenanceException(int assetId, AssetStatus status)
        : base($"Tài sản Id={assetId} đang ở trạng thái {status}, không thể tạo phiếu bảo trì (chỉ InUse hoặc Broken).")
    {
    }
}

// UC-12: phiếu đã Resolved/Failed là trạng thái cuối — không cho đổi trạng thái hay gán lại.
public class TicketStatusTransitionNotAllowedException : Exception
{
    public TicketStatusTransitionNotAllowedException(int id, TicketStatus status)
        : base($"Phiếu bảo trì Id={id} đang ở trạng thái {status}; chỉ phiếu Pending mới được xử lý.")
    {
    }
}

// UC-12 E1 và quy tắc "Technician chỉ được tự nhận phiếu cho chính mình".
public class TicketAssignmentForbiddenException : Exception
{
    public TicketAssignmentForbiddenException(string message) : base(message)
    {
    }
}

public class TicketAlreadyAssignedException : Exception
{
    public TicketAlreadyAssignedException(int id)
        : base($"Phiếu bảo trì Id={id} đã có kỹ thuật viên nhận.")
    {
    }
}
