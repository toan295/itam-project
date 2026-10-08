using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ITAM.API.Services.Implementations;

// Mật khẩu mặc định là chuỗi dùng chung mà Admin IT phải đọc lại được để báo người dùng, nên không băm một chiều được.
// Giá trị được MÃ HOÁ (Data Protection, xem DefaultPasswordCipher) khi lưu DB. Mỗi tài khoản vẫn chỉ lưu hash BCrypt;
// khi người dùng đổi mật khẩu thì giá trị này không còn liên quan tới họ. Audit log KHÔNG bao giờ ghi giá trị mật khẩu.
public class DefaultPasswordService : IDefaultPasswordService
{
    public const string SettingKey = "DefaultPassword";

    private readonly ISystemSettingRepository _repo;
    private readonly IAuditLogService _auditLogService;
    private readonly DefaultPasswordCipher _cipher;
    private readonly ILogger<DefaultPasswordService> _logger;

    public DefaultPasswordService(
        ISystemSettingRepository repo,
        IAuditLogService auditLogService,
        DefaultPasswordCipher cipher,
        ILogger<DefaultPasswordService> logger)
    {
        _repo = repo;
        _auditLogService = auditLogService;
        _cipher = cipher;
        _logger = logger;
    }

    public async Task<DefaultPasswordDto> GetAsync()
    {
        var plain = await ReadPlainAsync();
        return new DefaultPasswordDto { IsConfigured = plain is not null, Password = plain };
    }

    public async Task<DefaultPasswordDto> SetAsync(string password, int currentUserId)
    {
        var error = PasswordPolicy.Validate(password);
        if (error is not null || password != password.Trim())
        {
            throw new ArgumentException(error ?? "Mật khẩu mặc định không được bắt đầu hoặc kết thúc bằng khoảng trắng.");
        }

        var protectedValue = _cipher.Protect(password);
        var setting = await _repo.GetByKeyAsync(SettingKey);
        var existed = setting is not null;
        if (setting is null)
        {
            setting = new SystemSetting { Key = SettingKey, Value = protectedValue, UpdatedAt = DateTime.UtcNow };
            await _repo.AddAsync(setting);
            await _repo.SaveChangesAsync(); // cần Id để ghi audit log.
        }
        else
        {
            setting.Value = protectedValue;
            setting.UpdatedAt = DateTime.UtcNow;
            _repo.Update(setting);
        }

        await _auditLogService.RecordAsync(
            currentUserId,
            existed ? "Update" : "Create",
            "DefaultPassword",
            setting.Id,
            oldValue: new { IsConfigured = existed },
            newValue: new { IsConfigured = true });
        await _repo.SaveChangesAsync();

        return new DefaultPasswordDto { IsConfigured = true, Password = password };
    }

    public async Task ClearAsync(int currentUserId)
    {
        var setting = await _repo.GetByKeyAsync(SettingKey);
        if (setting is null)
        {
            return; // xoá khi chưa có -> idempotent.
        }

        var id = setting.Id;
        _repo.Remove(setting);
        await _auditLogService.RecordAsync(
            currentUserId, "Delete", "DefaultPassword", id,
            oldValue: new { IsConfigured = true }, newValue: new { IsConfigured = false });
        await _repo.SaveChangesAsync();
    }

    public async Task<string> GetRequiredAsync() =>
        await ReadPlainAsync() ?? throw new DefaultPasswordNotConfiguredException();

    public async Task<bool> IsDefaultPasswordAsync(string candidate)
    {
        var plain = await ReadPlainAsync();
        return plain is not null && string.Equals(plain, candidate, StringComparison.Ordinal);
    }

    // Nâng cấp giá trị cũ lưu văn bản thuần lên dạng mã hoá (chạy lúc khởi động, idempotent).
    public async Task EnsureEncryptedAsync()
    {
        var setting = await _repo.GetByKeyAsync(SettingKey);
        if (setting is null || DefaultPasswordCipher.IsProtected(setting.Value))
        {
            return;
        }

        setting.Value = _cipher.Protect(setting.Value);
        setting.UpdatedAt = DateTime.UtcNow;
        _repo.Update(setting);
        await _repo.SaveChangesAsync();
        _logger.LogInformation("Đã mã hoá mật khẩu mặc định đang lưu dạng văn bản thuần.");
    }

    // null khi chưa cấu hình HOẶC không giải mã được (mất khoá) — trường hợp sau coi như chưa cấu hình để Admin IT nhập lại.
    private async Task<string?> ReadPlainAsync()
    {
        var setting = await _repo.GetByKeyAsync(SettingKey);
        if (setting is null)
        {
            return null;
        }

        var plain = _cipher.Unprotect(setting.Value);
        if (plain is null)
        {
            _logger.LogWarning(
                "Không giải mã được mật khẩu mặc định (khoá Data Protection bị mất hoặc thay đổi). Cần Admin IT nhập lại.");
        }

        return plain;
    }
}
