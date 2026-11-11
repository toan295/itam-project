namespace ITAM.API.Services.Implementations;

public class BackupNotFoundException : Exception
{
    public BackupNotFoundException(string? name) : base($"Không tìm thấy bản sao lưu \"{name}\".")
    {
    }
}

// Lỗi chạy sao lưu/phục hồi (thiếu công cụ, file không hợp lệ, MySQL báo lỗi...) — thông báo đã là tiếng Việt, trả thẳng cho Admin IT.
public class BackupOperationException : Exception
{
    public BackupOperationException(string message) : base(message)
    {
    }
}
