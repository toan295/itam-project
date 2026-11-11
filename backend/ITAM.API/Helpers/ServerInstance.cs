namespace ITAM.API.Helpers;

/// <summary>
/// Mã của LẦN CHẠY hiện tại của máy chủ (đổi mỗi khi chạy lại bằng dotnet run / khởi động lại dịch vụ).
/// Được gắn vào token đăng nhập: token của lần chạy trước bị từ chối nên sau khi chạy lại hệ thống, người dùng phải đăng nhập lại
/// thay vì vào thẳng tài khoản cũ. Token vẫn không tự hết hạn theo thời gian trong lúc máy chủ đang chạy.
/// </summary>
public static class ServerInstance
{
    public const string ClaimType = "sid";
    public static readonly string Id = Guid.NewGuid().ToString("N");
}
