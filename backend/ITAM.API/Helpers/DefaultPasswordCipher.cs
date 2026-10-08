using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ITAM.API.Helpers;

// Mã hoá mật khẩu mặc định khi lưu trong bảng SystemSettings (Admin IT vẫn đọc lại được để báo người dùng, nên
// không thể băm một chiều). Dùng ASP.NET Core Data Protection: khoá nằm ngoài DB, nên lộ DB không lộ mật khẩu.
// Production PHẢI lưu khoá bền vững (DataProtection:KeysPath, xem Program.cs/README) — mất khoá thì giá trị
// đã mã hoá không giải mã được và Admin IT phải nhập lại mật khẩu mặc định.
public class DefaultPasswordCipher
{
    // Tiền tố đánh dấu giá trị đã mã hoá; giá trị không có tiền tố là dữ liệu cũ (văn bản thuần) cần nâng cấp.
    private const string Prefix = "enc:v1:";
    private const string Purpose = "EAIMS.SystemSettings.DefaultPassword";

    private readonly IDataProtector _protector;

    public DefaultPasswordCipher(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public static bool IsProtected(string stored) => stored.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plain) => Prefix + _protector.Protect(plain);

    // Trả về null khi không giải mã được (khoá bị mất/đổi, dữ liệu hỏng). Giá trị cũ dạng văn bản thuần được trả nguyên.
    public string? Unprotect(string stored)
    {
        if (!IsProtected(stored))
        {
            return stored;
        }

        try
        {
            return _protector.Unprotect(stored[Prefix.Length..]);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
