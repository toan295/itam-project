namespace ITAM.API.Services.Implementations;

public class UserNotFoundException : Exception
{
    public UserNotFoundException(int id) : base($"Không tìm thấy người dùng Id={id}.")
    {
    }
}

// UC-03: "không được tự khoá tài khoản của chính mình".
public class SelfLockNotAllowedException : Exception
{
    public SelfLockNotAllowedException() : base("Bạn không thể tự khoá tài khoản của chính mình.")
    {
    }
}

// Bảo vệ hệ thống khỏi bị khoá chết hoàn toàn: không cho khoá hoặc hạ quyền Admin IT đang active
// cuối cùng — nếu không, sẽ không còn ai có quyền quản trị để khôi phục lại.
public class LastAdminProtectionException : Exception
{
    public LastAdminProtectionException()
        : base("Đây là Admin IT đang hoạt động cuối cùng trong hệ thống — không thể khoá hoặc đổi sang vai trò khác.")
    {
    }
}

// Admin IT không tự "đặt lại mật khẩu hộ" cho chính mình qua kênh quản trị — phải dùng
// /auth/change-password (khi còn nhớ mật khẩu) hoặc /auth/forgot-password (khi quên).
public class SelfPasswordResetNotAllowedException : Exception
{
    public SelfPasswordResetNotAllowedException()
        : base("Không thể tự đặt lại mật khẩu của chính mình qua chức năng quản trị. " +
               "Hãy dùng \"Đổi mật khẩu\" (nếu còn nhớ mật khẩu hiện tại) hoặc \"Quên mật khẩu\" ở trang đăng nhập.")
    {
    }
}
