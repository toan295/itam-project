namespace ITAM.API.Models.DTOs.Backups;

public class BackupFileDto
{
    public string Name { get; set; } = default!;
    public long SizeBytes { get; set; }

    // Giờ Việt Nam (không kèm múi giờ).
    public DateTime CreatedAt { get; set; }

    // Manual = tạo bằng nút Sao lưu; PreRestore = hệ thống tự tạo ngay trước khi phục hồi; Uploaded = tải lên từ máy khác.
    public string Kind { get; set; } = "Manual";
}

public class RestoreBackupRequestDto
{
    // Phải gõ đúng "KHOI PHUC" để xác nhận (phục hồi ghi đè toàn bộ dữ liệu hiện tại).
    public string? ConfirmText { get; set; }
}

public class RestoreResultDto
{
    public string RestoredFrom { get; set; } = default!;

    // Bản sao lưu dữ liệu TRƯỚC khi phục hồi — dùng để quay lại nếu phục hồi nhầm.
    public string SafetyBackup { get; set; } = default!;
}
