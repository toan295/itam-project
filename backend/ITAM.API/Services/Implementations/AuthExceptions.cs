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

public class InvalidResetTokenException : Exception
{
    public InvalidResetTokenException(string message) : base(message)
    {
    }
}
