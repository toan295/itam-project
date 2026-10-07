namespace ITAM.API.Services.Implementations;

public class AuditLogNotFoundException : Exception
{
    public AuditLogNotFoundException(long id)
        : base($"Không tìm thấy nhật ký hệ thống Id={id}.")
    {
    }
}
