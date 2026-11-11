using ITAM.Tests.Helpers;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAM.Tests;

public class SoftwareLicenseServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenLicenseKeyExists_ThrowsInvalidOperationException()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 2));
        var service = CreateService(repository);

        var dto = new CreateSoftwareLicenseDto
        {
            SoftwareName = "Microsoft 365",
            LicenseKey = "KEY-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            MaxUsage = 2
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task AssignAsync_WhenLicenseReachedMaxUsage_ThrowsInvalidOperationException()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 2));
        repository.SeedAsset(10);
        repository.SeedAsset(11);
        repository.SeedAsset(12);
        repository.SeedAssignment(1, 10);
        repository.SeedAssignment(1, 11);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(1, 12));
    }

    [Fact]
    public async Task AssignAsync_WhenSameAssetAlreadyAssigned_ThrowsInvalidOperationException()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 5));
        repository.SeedAsset(10);
        repository.SeedAssignment(1, 10);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(1, 10));
    }

    [Fact]
    public async Task UnassignAsync_WhenAssignmentExists_DecreasesCurrentUsage()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 2));
        repository.SeedAsset(10);
        repository.SeedAsset(11);
        repository.SeedAssignment(1, 10);
        repository.SeedAssignment(1, 11);
        var service = CreateService(repository);

        await service.UnassignAsync(1, 11);
        var result = await service.GetByIdAsync(1);

        Assert.Equal(1, result.CurrentUsage);
    }

    [Fact]
    public async Task DeleteAsync_WhenLicenseIsAssignedToAssets_ThrowsInvalidOperationException()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 5));
        repository.SeedAsset(10);
        repository.SeedAssignment(1, 10);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
    }

    [Fact]
    public async Task DeleteAsync_WhenLicenseHasNoAssignments_DeletesSuccessfully()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(id: 1, licenseKey: "KEY-001", maxUsage: 5));
        var service = CreateService(repository);

        await service.DeleteAsync(1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByIdAsync(1));
    }

    [Fact]
    public async Task GetPagedAsync_AdvancedFilters_ArePassedToRepositoryParsed()
    {
        var repository = new FakeSoftwareLicenseRepository();
        var service = CreateService(repository);

        await service.GetPagedAsync(1, 20, "office", "expiringsoon", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "usage");

        var filter = repository.LastFilter!;
        Assert.Equal("office", filter.Search);
        Assert.Equal(LicenseStateFilter.ExpiringSoon, filter.State);
        Assert.Equal(LicenseSortBy.Usage, filter.SortBy);
        Assert.Equal(filter.Today.AddDays(30), filter.WarningDate);
    }

    [Theory]
    [InlineData("1", null)]
    [InlineData("Nope", null)]
    [InlineData(null, "Nope")]
    public async Task GetPagedAsync_InvalidStateOrSort_ThrowsArgumentException(string? state, string? sortBy)
    {
        var service = CreateService(new FakeSoftwareLicenseRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPagedAsync(1, 20, null, state, null, null, sortBy));
    }

    [Fact]
    public async Task GetPagedAsync_ExpiryFromAfterExpiryTo_ThrowsArgumentException()
    {
        var service = CreateService(new FakeSoftwareLicenseRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPagedAsync(1, 20, null, null, new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), null));
    }

    // ----- Quy tắc gán / trạng thái -----

    private static DateOnly Today => ITAM.API.Helpers.VietnamTime.Today();

    [Fact]
    public async Task AssignAsync_ExpiredLicense_IsRejectedAndNothingAssigned()
    {
        var repository = new FakeSoftwareLicenseRepository();
        var license = CreateLicense(1, "KEY-EXP", 5);
        license.ExpiryDate = Today.AddDays(-1);
        repository.SeedLicense(license);
        repository.SeedAsset(10);
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(1, 10));

        Assert.Contains("hết hạn", ex.Message);
        Assert.Equal(0, await repository.GetCurrentUsageAsync(1));
    }

    [Fact]
    public async Task AssignAsync_LicenseExpiringToday_StillAssignable()
    {
        var repository = new FakeSoftwareLicenseRepository();
        var license = CreateLicense(1, "KEY-TODAY", 5);
        license.ExpiryDate = Today;
        repository.SeedLicense(license);
        repository.SeedAsset(10);
        var service = CreateService(repository);

        var result = await service.AssignAsync(1, 10);

        Assert.Equal(1, result.CurrentUsage);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public async Task AssignAsync_DisposedAsset_IsRejected()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(1, "KEY-1", 5));
        repository.SeedDisposedAsset(10);
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(1, 10));

        Assert.Contains("thanh lý", ex.Message);
    }

    [Theory]
    [InlineData(5, 3, "Active", false, false, 2)]
    [InlineData(5, 4, "NearLimit", true, false, 1)]
    [InlineData(5, 5, "Full", false, true, 0)]
    [InlineData(1, 0, "Active", false, false, 1)]
    [InlineData(1, 1, "Full", false, true, 0)]
    [InlineData(10, 8, "NearLimit", true, false, 2)]
    [InlineData(10, 7, "Active", false, false, 3)]
    public async Task GetByIdAsync_UsageStatus_FollowsThresholds(int max, int used, string status, bool near, bool full, int remaining)
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(1, "KEY-1", max));
        for (var i = 0; i < used; i++)
        {
            repository.SeedAssignment(1, 100 + i);
        }

        var result = await CreateService(repository).GetByIdAsync(1);

        Assert.Equal(status, result.Status);
        Assert.Equal(near, result.IsNearUsageLimit);
        Assert.Equal(full, result.IsFull);
        Assert.Equal(remaining, result.RemainingSlots);
    }

    [Fact]
    public async Task GetByIdAsync_ExpiredBeatsFull_InStatus()
    {
        var repository = new FakeSoftwareLicenseRepository();
        var license = CreateLicense(1, "KEY-1", 1);
        license.ExpiryDate = Today.AddDays(-5);
        repository.SeedLicense(license);
        repository.SeedAssignment(1, 10);

        var result = await CreateService(repository).GetByIdAsync(1);

        Assert.Equal("Expired", result.Status);
        Assert.True(result.IsFull);
    }

    [Fact]
    public async Task UpdateAsync_RunsInsideLicenseLockAndRejectsLoweringBelowUsage()
    {
        var repository = new FakeSoftwareLicenseRepository();
        repository.SeedLicense(CreateLicense(1, "KEY-1", 5));
        for (var i = 0; i < 4; i++) repository.SeedAssignment(1, 100 + i);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(1, new UpdateSoftwareLicenseDto
        {
            SoftwareName = "X", LicenseKey = "KEY-1", ExpiryDate = Today.AddYears(1), MaxUsage = 3,
        }));
    }

    private static SoftwareLicenseService CreateService(ISoftwareLicenseRepository repository, RecordingExclusiveSection? exclusive = null)
    {
        return new SoftwareLicenseService(
            repository,
            exclusive ?? new RecordingExclusiveSection(),
            NullLogger<SoftwareLicenseService>.Instance);
    }

    private static SoftwareLicense CreateLicense(int id, string licenseKey, int maxUsage)
    {
        return new SoftwareLicense
        {
            Id = id,
            SoftwareName = "Test Software",
            LicenseKey = licenseKey,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            MaxUsage = maxUsage
        };
    }

    private sealed class FakeSoftwareLicenseRepository : ISoftwareLicenseRepository
    {
        private readonly List<SoftwareLicense> _licenses = new();
        private readonly HashSet<int> _assetIds = new();
        private readonly List<AssetSoftwareLicense> _assignments = new();
        private int _nextLicenseId = 100;
        private int _nextAssignmentId = 100;

        public SoftwareLicenseFilter? LastFilter { get; private set; }

        public void SeedLicense(SoftwareLicense license) => _licenses.Add(license);
        public void SeedAsset(int assetId) => _assetIds.Add(assetId);

        public void SeedAssignment(int licenseId, int assetId)
        {
            _assignments.Add(new AssetSoftwareLicense
            {
                Id = _nextAssignmentId++,
                LicenseId = licenseId,
                AssetId = assetId,
                AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        }

        public Task<(IReadOnlyList<SoftwareLicenseSnapshot> Items, int TotalItems)> GetPagedAsync(
            int page,
            int pageSize,
            SoftwareLicenseFilter filter)
        {
            var search = filter.Search;
            IEnumerable<SoftwareLicense> query = _licenses;
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.SoftwareName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.LicenseKey.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            LastFilter = filter;

            var list = query.ToList();
            IReadOnlyList<SoftwareLicenseSnapshot> items = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToSnapshot)
                .ToList();

            return Task.FromResult((items, list.Count));
        }

        public Task<SoftwareLicenseSnapshot?> GetByIdAsync(int id)
        {
            var license = _licenses.SingleOrDefault(item => item.Id == id);
            return Task.FromResult(license is null ? null : ToSnapshot(license));
        }

        public Task<SoftwareLicense?> GetEntityByIdAsync(int id)
        {
            return Task.FromResult(_licenses.SingleOrDefault(item => item.Id == id));
        }

        public Task<IReadOnlyList<SoftwareLicenseSnapshot>> GetExpiringSoonAsync(
            DateOnly fromDate,
            DateOnly toDate)
        {
            IReadOnlyList<SoftwareLicenseSnapshot> result = _licenses
                .Where(item => item.ExpiryDate >= fromDate && item.ExpiryDate <= toDate)
                .Select(ToSnapshot)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<bool> LicenseKeyExistsAsync(string licenseKey, int? excludeId = null)
        {
            var exists = _licenses.Any(item =>
                item.LicenseKey == licenseKey &&
                (!excludeId.HasValue || item.Id != excludeId.Value));
            return Task.FromResult(exists);
        }

        public Task<bool> AssetExistsAsync(int assetId) => Task.FromResult(_assetIds.Contains(assetId));

        private readonly HashSet<int> _disposedAssetIds = new();
        public void SeedDisposedAsset(int assetId) { _assetIds.Add(assetId); _disposedAssetIds.Add(assetId); }
        public Task<bool> IsAssetDisposedAsync(int assetId) => Task.FromResult(_disposedAssetIds.Contains(assetId));

        public Task<IReadOnlyList<AssetSoftwareLicense>> GetAssignmentsAsync(int licenseId)
        {
            IReadOnlyList<AssetSoftwareLicense> list = _assignments.Where(a => a.LicenseId == licenseId).ToList();
            return Task.FromResult(list);
        }

        public Task<bool> IsAssignedAsync(int licenseId, int assetId)
        {
            return Task.FromResult(_assignments.Any(item =>
                item.LicenseId == licenseId && item.AssetId == assetId));
        }

        public Task<int> GetCurrentUsageAsync(int licenseId)
        {
            return Task.FromResult(_assignments.Count(item => item.LicenseId == licenseId));
        }

        public Task AddAsync(SoftwareLicense license)
        {
            if (license.Id == 0)
            {
                license.Id = _nextLicenseId++;
            }

            _licenses.Add(license);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SoftwareLicense license) => Task.CompletedTask;

        public Task DeleteAsync(SoftwareLicense license)
        {
            _licenses.Remove(license);
            _assignments.RemoveAll(item => item.LicenseId == license.Id);
            return Task.CompletedTask;
        }

        public Task AssignAsync(AssetSoftwareLicense assignment)
        {
            assignment.Id = _nextAssignmentId++;
            _assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<bool> UnassignAsync(int licenseId, int assetId)
        {
            var assignment = _assignments.SingleOrDefault(item =>
                item.LicenseId == licenseId && item.AssetId == assetId);
            if (assignment is null)
            {
                return Task.FromResult(false);
            }

            _assignments.Remove(assignment);
            return Task.FromResult(true);
        }

        private SoftwareLicenseSnapshot ToSnapshot(SoftwareLicense license)
        {
            return new SoftwareLicenseSnapshot
            {
                Id = license.Id,
                SoftwareName = license.SoftwareName,
                LicenseKey = license.LicenseKey,
                ExpiryDate = license.ExpiryDate,
                MaxUsage = license.MaxUsage,
                Notes = license.Notes,
                CurrentUsage = _assignments.Count(item => item.LicenseId == license.Id)
            };
        }
    }
}
