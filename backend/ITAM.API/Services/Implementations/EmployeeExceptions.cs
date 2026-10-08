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
