namespace ITAM.API.Services.Implementations;

// Bản ghi đã bị người khác sửa kể từ lúc client tải nó (Version không còn khớp) — trả 409, client phải tải lại.
public class ConcurrencyConflictException : Exception
{
    public const string DefaultMessage =
        "Dữ liệu này vừa được người khác thay đổi. Vui lòng tải lại để xem bản mới nhất rồi thao tác lại.";

    public ConcurrencyConflictException() : base(DefaultMessage)
    {
    }

    // So số phiên bản client gửi với bản ghi hiện tại; client không gửi (null) thì bỏ qua kiểm tra.
    public static void EnsureCurrent(int currentVersion, int? clientVersion)
    {
        if (clientVersion.HasValue && clientVersion.Value != currentVersion)
        {
            throw new ConcurrencyConflictException();
        }
    }
}
