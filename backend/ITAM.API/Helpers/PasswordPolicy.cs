namespace ITAM.API.Helpers;

// Chính sách mật khẩu dùng chung: mật khẩu người dùng tự đặt, mật khẩu mặc định và mật khẩu Admin IT khởi tạo.
// BCrypt chỉ xử lý tối đa 72 byte nên giới hạn trên là 72 ký tự.
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 72;

    // Trả về thông báo lỗi tiếng Việt, hoặc null nếu hợp lệ.
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Mật khẩu không được để trống.";
        }

        if (password.Length < MinLength)
        {
            return $"Mật khẩu phải có ít nhất {MinLength} ký tự.";
        }

        if (password.Length > MaxLength)
        {
            return $"Mật khẩu không được vượt quá {MaxLength} ký tự.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "Mật khẩu phải gồm cả chữ và số.";
        }

        return null;
    }
}
