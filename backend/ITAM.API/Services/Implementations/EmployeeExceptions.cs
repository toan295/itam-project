namespace ITAM.API.Services.Implementations;

public class EmployeeNotFoundException : Exception
{
    public EmployeeNotFoundException(int id) : base($"Không tìm thấy nhân viên Id={id}.")
    {
    }
}

// Trùng tên trong phòng ban, nhân viên đang giữ tài sản, nhân viên đã có lịch sử phân bổ... — 409.
public class EmployeeConflictException : Exception
{
    public EmployeeConflictException(string message) : base(message)
    {
    }
}

// Admin ngừng hoạt động hồ sơ gắn với tài khoản của chính mình sẽ tự khoá mình — 403 (cùng quy tắc "không tự khoá" ở màn Người dùng).
public class EmployeeSelfDeactivationException : Exception
{
    public EmployeeSelfDeactivationException()
        : base("Bạn không thể ngừng hoạt động hồ sơ gắn với tài khoản của chính mình (tài khoản sẽ bị khoá).")
    {
    }
}
