namespace ITAM.API.Configurations;

public class BackupSettings
{
    public const string SectionName = "Backup";

    // Thư mục lưu file sao lưu (tương đối so với thư mục chạy API, hoặc đường dẫn tuyệt đối).
    public string Directory { get; set; } = "App_Data/Backups";

    // Thư mục chứa mysqldump / mysql. Bỏ trống = tìm trong PATH và các thư mục cài MySQL phổ biến.
    public string? MysqlBinPath { get; set; }

    // Thời gian tối đa cho một lần sao lưu / phục hồi.
    public int TimeoutSeconds { get; set; } = 600;
}
