using ITAM.API.Models.DTOs.Common;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class SoftwareLicenseService : ISoftwareLicenseService
{
    private const int DefaultWarningDays = 30;
    private const decimal NearUsageLimitPercentage = 80m;

    private readonly ISoftwareLicenseRepository _repository;
    private readonly IExclusiveSection _exclusive;
    private readonly ILogger<SoftwareLicenseService> _logger;

    public SoftwareLicenseService(
        ISoftwareLicenseRepository repository,
        IExclusiveSection exclusive,
        ILogger<SoftwareLicenseService> logger)
    {
        _repository = repository;
        _exclusive = exclusive;
        _logger = logger;
    }

    public async Task<PagedResultDto<SoftwareLicenseResponseDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        var (items, totalItems) = await _repository.GetPagedAsync(page, pageSize, search);

        return new PagedResultDto<SoftwareLicenseResponseDto>
        {
            Items = items.Select(item => MapToResponse(item, DefaultWarningDays)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
        };
    }

    public async Task<SoftwareLicenseResponseDto> GetByIdAsync(int id)
    {
        var license = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");

        return MapToResponse(license, DefaultWarningDays);
    }

    public async Task<SoftwareLicenseResponseDto> CreateAsync(CreateSoftwareLicenseDto dto)
    {
        var licenseKey = dto.LicenseKey.Trim();
        if (await _repository.LicenseKeyExistsAsync(licenseKey))
        {
            throw new InvalidOperationException($"LicenseKey '{licenseKey}' đã tồn tại.");
        }

        var license = new SoftwareLicense
        {
            SoftwareName = dto.SoftwareName.Trim(),
            LicenseKey = licenseKey,
            ExpiryDate = dto.ExpiryDate,
            MaxUsage = dto.MaxUsage,
            Notes = NormalizeOptionalText(dto.Notes)
        };

        await _repository.AddAsync(license);
        _logger.LogInformation("Created software license {LicenseId} ({SoftwareName})", license.Id, license.SoftwareName);

        return await GetByIdAsync(license.Id);
    }

    public async Task<SoftwareLicenseResponseDto> UpdateAsync(int id, UpdateSoftwareLicenseDto dto)
    {
        var license = await _repository.GetEntityByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");

        var licenseKey = dto.LicenseKey.Trim();
        if (await _repository.LicenseKeyExistsAsync(licenseKey, id))
        {
            throw new InvalidOperationException($"LicenseKey '{licenseKey}' đã tồn tại.");
        }

        var currentUsage = await _repository.GetCurrentUsageAsync(id);
        if (dto.MaxUsage < currentUsage)
        {
            throw new InvalidOperationException(
                $"MaxUsage mới ({dto.MaxUsage}) không được nhỏ hơn số license đang sử dụng ({currentUsage}).");
        }

        license.SoftwareName = dto.SoftwareName.Trim();
        license.LicenseKey = licenseKey;
        license.ExpiryDate = dto.ExpiryDate;
        license.MaxUsage = dto.MaxUsage;
        license.Notes = NormalizeOptionalText(dto.Notes);

        await _repository.UpdateAsync(license);
        _logger.LogInformation("Updated software license {LicenseId}", id);

        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var license = await _repository.GetEntityByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");

        // Không xoá License đang được gán cho tài sản — FK giờ là Restrict (không còn Cascade), nên
        // nếu bỏ qua bước kiểm tra này thì lỗi khoá ngoại từ MySQL sẽ lộ ra ngoài dưới dạng 500 thay
        // vì 409 thân thiện. Admin IT phải Gỡ hết rồi mới Xoá được, tránh mất lịch sử gán ngoài ý muốn.
        var currentUsage = await _repository.GetCurrentUsageAsync(id);
        if (currentUsage > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xoá license đang được gán cho {currentUsage} tài sản. Hãy gỡ hết trước khi xoá.");
        }

        await _repository.DeleteAsync(license);
        _logger.LogInformation("Deleted software license {LicenseId}", id);
    }

    // Khoá dòng license: đếm số lượt dùng rồi mới gán nên phải tuần tự, nếu không nhiều request đồng thời cùng thấy
    // "còn chỗ" và gán vượt MaxUsage.
    public Task<SoftwareLicenseResponseDto> AssignAsync(int id, int assetId) =>
        _exclusive.RunAsync(LockTarget.SoftwareLicense, id, () => AssignCoreAsync(id, assetId));

    private async Task<SoftwareLicenseResponseDto> AssignCoreAsync(int id, int assetId)
    {
        var license = await _repository.GetEntityByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");

        if (!await _repository.AssetExistsAsync(assetId))
        {
            throw new KeyNotFoundException($"Không tìm thấy tài sản có AssetId = {assetId}.");
        }

        if (await _repository.IsAssignedAsync(id, assetId))
        {
            throw new InvalidOperationException(
                $"License Id = {id} đã được gán cho AssetId = {assetId}, không thể gán trùng.");
        }

        var currentUsage = await _repository.GetCurrentUsageAsync(id);
        if (currentUsage >= license.MaxUsage)
        {
            throw new InvalidOperationException(
                $"License đã đạt giới hạn sử dụng ({currentUsage}/{license.MaxUsage}), không thể gán thêm.");
        }

        await _repository.AssignAsync(new AssetSoftwareLicense
        {
            LicenseId = id,
            AssetId = assetId,
            AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        _logger.LogInformation("Assigned software license {LicenseId} to asset {AssetId}", id, assetId);
        return await GetByIdAsync(id);
    }

    public async Task UnassignAsync(int id, int assetId)
    {
        if (await _repository.GetEntityByIdAsync(id) is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");
        }

        var removed = await _repository.UnassignAsync(id, assetId);
        if (!removed)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy gán license Id = {id} cho AssetId = {assetId}.");
        }

        _logger.LogInformation("Unassigned software license {LicenseId} from asset {AssetId}", id, assetId);
    }

    public async Task<IReadOnlyList<SoftwareLicenseResponseDto>> GetExpiringSoonAsync(int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = today.AddDays(days);

        var licenses = await _repository.GetExpiringSoonAsync(today, endDate);
        return licenses.Select(item => MapToResponse(item, days)).ToList();
    }

    private static SoftwareLicenseResponseDto MapToResponse(
        SoftwareLicenseSnapshot license,
        int warningDays)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var warningDate = today.AddDays(warningDays);
        var usagePercentage = license.MaxUsage <= 0
            ? 0m
            : Math.Round(license.CurrentUsage * 100m / license.MaxUsage, 2);

        return new SoftwareLicenseResponseDto
        {
            Id = license.Id,
            SoftwareName = license.SoftwareName,
            LicenseKey = license.LicenseKey,
            ExpiryDate = license.ExpiryDate,
            MaxUsage = license.MaxUsage,
            CurrentUsage = license.CurrentUsage,
            UsagePercentage = usagePercentage,
            IsExpired = license.ExpiryDate < today,
            IsExpiringSoon = license.ExpiryDate >= today && license.ExpiryDate <= warningDate,
            IsNearUsageLimit = usagePercentage >= NearUsageLimitPercentage,
            Notes = license.Notes
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
