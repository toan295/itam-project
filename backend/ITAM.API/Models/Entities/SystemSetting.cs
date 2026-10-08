namespace ITAM.API.Models.Entities;

// Cấu hình hệ thống dạng key-value do Admin IT chỉnh được lúc chạy (hiện chỉ dùng cho mật khẩu mặc định).
public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
