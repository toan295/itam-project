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
            string? search = null)
        {
            IEnumerable<SoftwareLicense> query = _licenses;
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.SoftwareName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.LicenseKey.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

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
