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
