namespace ITAM.API.Services.Implementations;

public class ActivationLinkInvalidException : Exception
{
    public ActivationLinkInvalidException() : base("Liên kết kích hoạt không hợp lệ.")
    {
    }
}

// Liên kết hợp lệ nhưng không còn dùng được: hết hạn, đã dùng, bị thay bằng email mới hơn, hoặc tài khoản đã bị khoá.
public class ActivationLinkUnusableException : Exception
{
    public ActivationLinkUnusableException(string message) : base(message)
    {
    }
}

public class ActivationResendTooSoonException : Exception
{
    public ActivationResendTooSoonException(int waitSeconds)
        : base($"Vừa gửi email kích hoạt xong. Vui lòng thử lại sau {waitSeconds} giây.")
    {
    }
}
