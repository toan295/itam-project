namespace ITAM.API.Services.Implementations;

public class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException(string email) : base($"Email '{email}' đã được sử dụng.")
    {
    }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Email hoặc mật khẩu không đúng.")
    {
    }
}

public class AccountLockedException : Exception
{
    public AccountLockedException() : base("Tài khoản đã bị khoá. Vui lòng liên hệ Admin IT.")
    {
    }
}

public class TooManyLoginAttemptsException : Exception
{
    public TimeSpan RetryAfter { get; }

    public TooManyLoginAttemptsException(TimeSpan retryAfter)
        : base($"Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes))} phút.")
    {
        RetryAfter = retryAfter;
    }
}
