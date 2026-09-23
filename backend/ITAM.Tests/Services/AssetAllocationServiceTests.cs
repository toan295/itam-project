using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class AssetAllocationServiceTests
{
    private readonly Mock<IAssetAllocationRepository> _allocationRepository = new();
    private readonly Mock<IAssetRepository> _assetRepository = new();

    [Fact]
    public async Task CreateAsync_AssetNotInUse_ThrowsAssetNotAvailableForAllocationException()
    {
        var asset = CreateAsset(status: AssetStatus.Broken);
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);

        var service = CreateService();

        await Assert.ThrowsAsync<AssetNotAvailableForAllocationException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, asset.DepartmentId), "Admin IT", 1));
    }

    [Fact]
    public async Task CreateAsync_AssetAlreadyHasOpenAllocation_ThrowsAssetAlreadyAllocatedException()
    {
        var asset = CreateAsset();
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);
        _assetRepository.Setup(x => x.DepartmentExistsAsync(asset.DepartmentId)).ReturnsAsync(true);
        _allocationRepository.Setup(x => x.HasOpenAllocationAsync(asset.Id)).ReturnsAsync(true);

        var service = CreateService();

        await Assert.ThrowsAsync<AssetAlreadyAllocatedException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, asset.DepartmentId), "Admin IT", 1));
    }

    [Fact]
    public async Task CreateAsync_ManagerOutsideAssetDepartment_ThrowsDepartmentForbiddenException()
    {
        var asset = CreateAsset(departmentId: 2);
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);

        var service = CreateService();

        await Assert.ThrowsAsync<DepartmentForbiddenException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, asset.DepartmentId), "Manager", 1));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_Succeeds()
    {
        var asset = CreateAsset();
        AssetAllocation? created = null;

        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);
        _assetRepository.Setup(x => x.DepartmentExistsAsync(asset.DepartmentId)).ReturnsAsync(true);
        _allocationRepository.Setup(x => x.HasOpenAllocationAsync(asset.Id)).ReturnsAsync(false);
        _allocationRepository
            .Setup(x => x.AddAsync(It.IsAny<AssetAllocation>()))
            .Callback<AssetAllocation>(allocation =>
            {
                allocation.Id = 50;
                created = allocation;
            })
            .Returns(Task.CompletedTask);
        _allocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _allocationRepository
            .Setup(x => x.GetByIdWithDetailsAsync(50))
            .Returns(() => Task.FromResult<AssetAllocation?>(WithNavigation(created!, asset)));

        var service = CreateService();
        var result = await service.CreateAsync(
            CreateRequest(asset.Id, asset.DepartmentId),
            "Admin IT",
            1);

        Assert.Equal(50, result.Id);
        Assert.Equal(asset.Id, result.AssetId);
        Assert.Equal("Nguyen Van A", result.RecipientName);
        Assert.Equal("Allocated", result.Status);
        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Once);
    }

    [Fact]
    public async Task ReturnAsync_AllocationAlreadyReturned_ThrowsAllocationAlreadyReturnedException()
    {
        var allocation = CreateAllocation(status: AllocationStatus.Returned);
        allocation.ReturnedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);

        var service = CreateService();

        await Assert.ThrowsAsync<AllocationAlreadyReturnedException>(() =>
            service.ReturnAsync(allocation.Id, CreateReturnRequest("Good"), "Admin IT", 1));
    }

    [Fact]
    public async Task ReturnAsync_ConditionGood_DoesNotChangeAssetStatus()
    {
        var asset = CreateAsset();
        var allocation = CreateAllocation(assetId: asset.Id, departmentId: asset.DepartmentId);
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);
        _allocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _allocationRepository
            .Setup(x => x.GetByIdWithDetailsAsync(allocation.Id))
            .Returns(() => Task.FromResult<AssetAllocation?>(WithNavigation(allocation, asset)));

        var service = CreateService();
        var result = await service.ReturnAsync(
            allocation.Id,
            CreateReturnRequest("Good"),
            "Admin IT",
            1);

        Assert.Equal("Returned", result.Status);
        Assert.Equal("Good", result.ReturnCondition);
        Assert.Equal(AssetStatus.InUse, asset.Status);
        _assetRepository.Verify(x => x.Update(It.IsAny<Asset>()), Times.Never);
        _assetRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_ConditionDamaged_SetsAssetStatusToBroken()
    {
        var asset = CreateAsset();
        var allocation = CreateAllocation(assetId: asset.Id, departmentId: asset.DepartmentId);
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);
        _allocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);
        _assetRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _allocationRepository
            .Setup(x => x.GetByIdWithDetailsAsync(allocation.Id))
            .Returns(() => Task.FromResult<AssetAllocation?>(WithNavigation(allocation, asset)));

        var service = CreateService();
        await service.ReturnAsync(
            allocation.Id,
            CreateReturnRequest("Damaged"),
            "Admin IT",
            1);

        Assert.Equal(AssetStatus.Broken, asset.Status);
        _assetRepository.Verify(x => x.Update(asset), Times.Once);
        _assetRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    private AssetAllocationService CreateService() => new(
        _allocationRepository.Object,
        _assetRepository.Object,
        NullLogger<AssetAllocationService>.Instance);

    private static CreateAssetAllocationDto CreateRequest(int assetId, int departmentId) => new()
    {
        AssetId = assetId,
        DepartmentId = departmentId,
        RecipientName = "Nguyen Van A",
        AllocatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        HandoverNote = "Ban giao day du phu kien",
    };

    private static ReturnAssetAllocationDto CreateReturnRequest(string condition) => new()
    {
        ReturnedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Condition = condition,
        ReturnNote = "Da kiem tra khi nhan lai",
    };

    private static Asset CreateAsset(
        int id = 10,
        int departmentId = 1,
        AssetStatus status = AssetStatus.InUse) => new()
    {
        Id = id,
        AssetCode = $"ASSET-{id:000}",
        Name = "Laptop Test",
        CategoryId = 1,
        DepartmentId = departmentId,
        Status = status,
        Department = new Department { Id = departmentId, Name = $"Phong {departmentId}" },
        Category = new AssetCategory { Id = 1, Name = "Laptop" },
    };

    private static AssetAllocation CreateAllocation(
        int id = 20,
        int assetId = 10,
        int departmentId = 1,
        AllocationStatus status = AllocationStatus.Allocated) => new()
    {
        Id = id,
        AssetId = assetId,
        DepartmentId = departmentId,
        RecipientName = "Nguyen Van A",
        AllocatedDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10),
        Status = status,
    };

    private static AssetAllocation WithNavigation(AssetAllocation allocation, Asset asset)
    {
        allocation.Asset = asset;
        allocation.Department = new Department
        {
            Id = allocation.DepartmentId,
            Name = $"Phong {allocation.DepartmentId}",
        };
        return allocation;
    }
}
