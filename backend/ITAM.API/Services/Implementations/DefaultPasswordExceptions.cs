namespace ITAM.API.Services.Implementations;

public class DefaultPasswordNotConfiguredException : Exception
{
    public DefaultPasswordNotConfiguredException()
        : base("Chưa cấu hình mật khẩu mặc định. Vui lòng đặt mật khẩu mặc định trước khi cấp tài khoản hoặc đặt lại mật khẩu.")
    {
    }
}
