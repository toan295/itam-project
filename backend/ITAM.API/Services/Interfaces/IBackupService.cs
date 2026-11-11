using ITAM.API.Models.DTOs.Backups;

namespace ITAM.API.Services.Interfaces;

public interface IBackupService
{
    Task RecordDownloadAsync(int userId, string name);   // ghi nhật ký "Tải bản sao lưu về máy"
    Task<IReadOnlyList<BackupFileDto>> ListAsync();
    Task<BackupFileDto> CreateAsync(int userId);
    FileInfo GetFile(string name);
    Task<BackupFileDto> SaveUploadAsync(int userId, Stream content, long length);
    Task DeleteAsync(int userId, string name);

    // Ghi đè TOÀN BỘ dữ liệu hiện tại bằng bản sao lưu; tự tạo bản sao lưu an toàn trước.
    Task<RestoreResultDto> RestoreAsync(int userId, string name);
}
