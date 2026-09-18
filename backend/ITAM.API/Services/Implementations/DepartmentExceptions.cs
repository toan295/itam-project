namespace ITAM.API.Services.Implementations;

public class DepartmentNotFoundException : Exception
{
    public DepartmentNotFoundException(int id)
        : base($"Không tìm thấy phòng ban Id={id}.")
    {
    }
}

public class DepartmentNameAlreadyExistsException : Exception
{
    public DepartmentNameAlreadyExistsException(string name)
        : base($"Phòng ban '{name}' đã tồn tại.")
    {
    }
}
