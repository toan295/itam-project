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

// UC-04 E1: không xoá phòng ban đang được người dùng/tài sản/phân bổ/dự báo ngân sách tham chiếu.
public class DepartmentInUseException : Exception
{
    public DepartmentInUseException(int id)
        : base($"Không thể xoá phòng ban Id={id} vì vẫn còn người dùng, tài sản hoặc bản ghi phân bổ thuộc phòng ban này.")
    {
    }
}
