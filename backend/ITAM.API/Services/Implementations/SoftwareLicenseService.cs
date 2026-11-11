using ITAM.API.Models.DTOs.Common;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        string? search = null,
        string? state = null,
        DateOnly? expiryFrom = null,
        DateOnly? expiryTo = null,
        string? sortBy = null,
        string? assignment = null)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        if (expiryFrom.HasValue && expiryTo.HasValue && expiryFrom > expiryTo)
        {
            throw new ArgumentException("expiryFrom không được lớn hơn expiryTo.");
        }

        var today = VietnamTime.Today();
        var filter = new SoftwareLicenseFilter
        {
            Search = search,
            State = ParseEnumOrThrow<LicenseStateFilter>(state, "state"),
            ExpiryFrom = expiryFrom,
            ExpiryTo = expiryTo,
            SortBy = ParseEnumOrThrow<LicenseSortBy>(sortBy, "sortBy") ?? LicenseSortBy.Name,
            Assignment = ParseEnumOrThrow<LicenseAssignmentFilter>(assignment, "assignment"),
            Today = today,
            WarningDate = today.AddDays(DefaultWarningDays),
        };

        var (items, totalItems) = await _repository.GetPagedAsync(page, pageSize, filter);

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

    public async Task<IReadOnlyList<LicenseAssignmentDto>> GetAssignmentsAsync(int id)
    {
        if (await _repository.GetEntityByIdAsync(id) is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy license có Id = {id}.");
        }

        var assignments = await _repository.GetAssignmentsAsync(id);
        return assignments.Select(a => new LicenseAssignmentDto
        {
            AssetId = a.AssetId,
            AssetCode = a.Asset.AssetCode,
            AssetName = a.Asset.Name,
            DepartmentName = a.Asset.Department?.Name ?? string.Empty,
            AssetStatus = a.Asset.Status.ToString(),
            AssignedDate = a.AssignedDate,
        }).ToList();
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

        try
        {
            await _repository.AddAsync(license);
        }
        catch (DbUpdateException)
        {
            // Hai request cùng tạo một key: ràng buộc duy nhất ở database là chốt chặn cuối.
            throw new InvalidOperationException($"LicenseKey '{licenseKey}' đã tồn tại.");
        }

        _logger.LogInformation("Created software license {LicenseId} ({SoftwareName})", license.Id, license.SoftwareName);

        return await GetByIdAsync(license.Id);
    }

    // Sửa/xoá cũng vào cùng ổ khoá với gán: nếu không, MaxUsage có thể bị hạ xuống dưới số đang dùng ngay lúc có
    // một lượt gán đang chạy, hoặc license bị xoá giữa lúc đang gán.
    public Task<SoftwareLicenseResponseDto> UpdateAsync(int id, UpdateSoftwareLicenseDto dto) =>
        _exclusive.RunAsync(LockTarget.SoftwareLicense, id, () => UpdateCoreAsync(id, dto));

    private async Task<SoftwareLicenseResponseDto> UpdateCoreAsync(int id, UpdateSoftwareLicenseDto dto)
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

    public Task DeleteAsync(int id) =>
        _exclusive.RunAsync(LockTarget.SoftwareLicense, id, async () =>
        {
            await DeleteCoreAsync(id);
            return true;
        });

    private async Task DeleteCoreAsync(int id)
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

        // Quy tắc kiểm tra ở backend, không dựa vào việc giao diện có ẩn nút hay không.
        if (license.ExpiryDate < VietnamTime.Today())
        {
            throw new InvalidOperationException(
                $"License đã hết hạn ngày {license.ExpiryDate:dd/MM/yyyy}, không thể gán thêm. Hãy gia hạn (cập nhật ngày hết hạn) trước.");
        }

        if (await _repository.IsAssetDisposedAsync(assetId))
        {
            throw new InvalidOperationException("Tài sản đã được thanh lý, không thể gán license.");
        }

        if (await _repository.IsAssignedAsync(id, assetId))
        {
            throw new InvalidOperationException(
                "License này đã được gán cho tài sản đó (mỗi cặp license – tài sản chỉ gán một lần).");
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
            AssignedDate = VietnamTime.Today()
        });

        _logger.LogInformation("Assigned software license {LicenseId} to asset {AssetId}", id, assetId);
        return await GetByIdAsync(id);
    }

    public Task UnassignAsync(int id, int assetId) =>
        _exclusive.RunAsync(LockTarget.SoftwareLicense, id, async () =>
        {
            await UnassignCoreAsync(id, assetId);
            return true;
        });

    private async Task UnassignCoreAsync(int id, int assetId)
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
        var today = VietnamTime.Today();
        var endDate = today.AddDays(days);

        var licenses = await _repository.GetExpiringSoonAsync(today, endDate);
        return licenses.Select(item => MapToResponse(item, days)).ToList();
    }

    private static SoftwareLicenseResponseDto MapToResponse(
        SoftwareLicenseSnapshot license,
        int warningDays)
    {
        var today = VietnamTime.Today();
        var warningDate = today.AddDays(warningDays);
        var usagePercentage = license.MaxUsage <= 0
            ? 0m
            : Math.Round(license.CurrentUsage * 100m / license.MaxUsage, 2);

        var isExpired = license.ExpiryDate < today;
        var isFull = license.CurrentUsage >= license.MaxUsage;
        var isNearLimit = !isFull && usagePercentage >= NearUsageLimitPercentage;
        var isExpiringSoon = license.ExpiryDate >= today && license.ExpiryDate <= warningDate;

        return new SoftwareLicenseResponseDto
        {
            Id = license.Id,
            SoftwareName = license.SoftwareName,
            LicenseKey = license.LicenseKey,
            ExpiryDate = license.ExpiryDate,
            MaxUsage = license.MaxUsage,
            CurrentUsage = license.CurrentUsage,
            UsagePercentage = usagePercentage,
            IsExpired = isExpired,
            IsExpiringSoon = isExpiringSoon,
            IsNearUsageLimit = isNearLimit,
            IsFull = isFull,
            RemainingSlots = Math.Max(0, license.MaxUsage - license.CurrentUsage),
            Status = isExpired ? "Expired" : isFull ? "Full" : isNearLimit ? "NearLimit" : isExpiringSoon ? "ExpiringSoon" : "Active",
            Notes = license.Notes
        };
    }

    // Khớp đúng tên enum (không phân biệt hoa/thường) — Enum.TryParse còn nhận cả chuỗi số nên kiểm tra tên trước.
    private static TEnum? ParseEnumOrThrow<TEnum>(string? value, string paramName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Enum.GetNames<TEnum>().FirstOrDefault(n => string.Equals(n, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new ArgumentException($"{paramName} không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<TEnum>())}");
        }

        return Enum.Parse<TEnum>(match);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
