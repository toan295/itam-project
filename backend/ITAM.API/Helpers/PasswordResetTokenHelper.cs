using System.Security.Cryptography;
using System.Text;
using ITAM.API.Configurations;
using ITAM.API.Models.Entities;
using Microsoft.Extensions.Options;

namespace ITAM.API.Helpers;

public record ResetTokenPayload(int UserId, string PasswordFingerprint);

// Sinh & kiểm tra token đặt lại mật khẩu (UC quên mật khẩu) mà KHÔNG cần thêm cột DB mới —
// tái dùng JwtSettings.Secret sẵn có, ký bằng HMAC-SHA256 riêng (không đi qua JwtSecurityTokenHandler
// của Microsoft.IdentityModel), nên token này KHÔNG được JwtBearer middleware chấp nhận như một Bearer
// token thông thường — tránh bị lạm dụng để gọi API khác ngoài đúng endpoint reset-password.
//
// Token tự vô hiệu hoá ngay khi mật khẩu thực sự đổi: payload nhúng "fingerprint" (SHA-256 rút gọn)
// của PasswordHash hiện tại tại thời điểm cấp token; khi đối chiếu ở bước reset, nếu PasswordHash của
// user đã khác đi (đã đổi mật khẩu qua lần reset trước, hoặc do nguyên nhân khác) thì fingerprint
// không khớp nữa và token bị từ chối — coi như "dùng một lần" mà không cần lưu trạng thái đã dùng.
public class PasswordResetTokenHelper
{
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromMinutes(15);

    private readonly byte[] _key;
    private readonly TimeSpan _tokenLifetime;

    public PasswordResetTokenHelper(IOptions<JwtSettings> jwtSettings, TimeSpan? tokenLifetime = null)
    {
        _key = Encoding.UTF8.GetBytes(jwtSettings.Value.Secret + ":password-reset");
        // Tham số tokenLifetime chỉ dùng để unit test kiểm tra logic hết hạn mà không cần chờ thời
        // gian thật — DI trong Program.cs luôn dùng giá trị mặc định 15 phút.
        _tokenLifetime = tokenLifetime ?? DefaultTokenLifetime;
    }

    public string GenerateToken(User user, TimeSpan? lifetimeOverride = null)
    {
        // Link Admin IT cấp (tạo tài khoản mới / đặt lại mật khẩu hộ) dùng thời hạn dài hơn
        // (mặc định 24h ở nơi gọi) vì Admin có thể chưa chuyển ngay cho người dùng cuối
        // (gọi điện, gặp trực tiếp...) — khác với token tự phục vụ 15 phút (nhạy cảm hơn vì
        // sinh ra ẩn danh, cần dùng ngay).
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(lifetimeOverride ?? _tokenLifetime).ToUnixTimeSeconds();
        var fingerprint = ComputeFingerprint(user.PasswordHash);
        var payload = $"{user.Id}.{expiresAtUnix}.{fingerprint}";
        var signature = Sign(payload);
        return $"{UrlBase64Encode(Encoding.UTF8.GetBytes(payload))}.{UrlBase64Encode(signature)}";
    }

    public bool TryParse(string? token, out ResetTokenPayload? payload, out string error)
    {
        payload = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "Token không hợp lệ.";
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            error = "Token không hợp lệ.";
            return false;
        }

        string payloadText;
        byte[] signature;
        try
        {
            payloadText = Encoding.UTF8.GetString(UrlBase64Decode(parts[0]));
            signature = UrlBase64Decode(parts[1]);
        }
        catch (FormatException)
        {
            error = "Token không hợp lệ.";
            return false;
        }

        // So sánh chữ ký bằng thời gian cố định (constant-time) để tránh timing attack.
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payloadText)))
        {
            error = "Token không hợp lệ.";
            return false;
        }

        var segments = payloadText.Split('.');
        if (segments.Length != 3
            || !int.TryParse(segments[0], out var userId)
            || !long.TryParse(segments[1], out var expiresAtUnix))
        {
            error = "Token không hợp lệ.";
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            error = "Token đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới.";
            return false;
        }

        payload = new ResetTokenPayload(userId, segments[2]);
        error = "";
        return true;
    }

    public static string ComputeFingerprint(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..12];

    private byte[] Sign(string payload) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

    private static string UrlBase64Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] UrlBase64Decode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
