namespace ITAM.API.Services.Implementations;

public class DisposalRequestNotFoundException : Exception
{
    public DisposalRequestNotFoundException(int id) : base($"Không tìm thấy phiếu thanh lý Id={id}.")
    {
    }
}

public class DisposalStatusNotFoundException : Exception
{
    public DisposalStatusNotFoundException(int id) : base($"Không tìm thấy trạng thái Id={id}.")
    {
    }
}

// Sai bước của luồng (vd duyệt phiếu chưa đề xuất), tài sản đã có phiếu đang mở... — 409.
public class DisposalConflictException : Exception
{
    public DisposalConflictException(string message) : base(message)
    {
    }
}

// Vai trò hiện tại không được làm thao tác này — 403.
public class DisposalForbiddenException : Exception
{
    public DisposalForbiddenException(string message) : base(message)
    {
    }
}
