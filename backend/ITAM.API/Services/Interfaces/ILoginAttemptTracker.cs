namespace ITAM.API.Services.Interfaces;

// Chống dò mật khẩu theo từng cặp (địa chỉ IP, email): sai quá nhiều lần thì tạm chặn cặp đó. Khoá theo CẢ HAI để kẻ
// tấn công từ IP khác không khoá được người dùng thật (khác với khoá chỉ theo email), và người dùng khác cùng IP
// không bị liên đới (khác với giới hạn chỉ theo IP).
public interface ILoginAttemptTracker
{
    // true nếu cặp đang bị chặn; retryAfter là thời gian còn lại.
    bool IsBlocked(string key, out TimeSpan retryAfter);

    void RecordFailure(string key);

    void Reset(string key);
}
