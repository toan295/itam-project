namespace ITAM.API.Configurations;

// Cấu hình gửi email (mục "Email" trong appsettings). Thông tin đăng nhập SMTP đặt ở appsettings.Development.json
// (đã gitignore) hoặc biến môi trường Email__Password — KHÔNG commit vào repo.
public class EmailSettings
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "EAIMS";

    // Gốc URL của frontend, dùng dựng liên kết trong email: liên kết = {FrontendBaseUrl}/pages/activate-account.html?token=...
    // nên phải trỏ tới thư mục chứa `pages/` (serve riêng thư mục frontend: http://localhost:5500; serve cả repo: http://localhost:5500/frontend).
    public string FrontendBaseUrl { get; set; } = "http://localhost:5500";

    // Số ngày liên kết kích hoạt tài khoản còn hiệu lực (ngắn hơn: liên kết này cho phép đặt mật khẩu).
    public int ActivationValidDays { get; set; } = 3;

    public bool IsSmtpConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
