using ITAM.Tests.Helpers;
using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITAM.Tests.Services;

public class AssetAllocationServiceTests
{
    private readonly Mock<IAssetAllocationRepository> _allocationRepository = new();
    private readonly Mock<IAssetRepository> _assetRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly RecordingExclusiveSection _exclusive = new();

    // Quy ước test: EmployeeId = departmentId * 100 -> nhân viên (đang hoạt động) thuộc phòng ban departmentId.
    public AssetAllocationServiceTests()
    {
        _employeeRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => new Employee
            {
                Id = id,
                EmployeeCode = $"NV{id:D4}",
                FullName = "Nguyen Van A",
                DepartmentId = id / 100,
                Position = "Chuyên viên",
                IsActive = true,
                Department = new Department { Id = id / 100, Name = $"Phong {id / 100}" },
            });
    }

    // ----- CreateAsync (UC-14) -----

    [Fact]
    public async Task CreateAsync_AssetNotFound_ThrowsAssetNotFoundException()
    {
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(99)).ReturnsAsync((Asset?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<AssetNotFoundException>(() =>
            service.CreateAsync(CreateRequest(99, 1), "Admin IT", null));

        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AssetNotInUse_ThrowsAssetNotAvailableForAllocationException()
    {
        var asset = CreateAsset(status: AssetStatus.Broken);
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);

        var service = CreateService();

        await Assert.ThrowsAsync<AssetNotAvailableForAllocationException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, asset.DepartmentId), "Admin IT", 1));

        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmployeeDoesNotExist_ThrowsArgumentException()
    {
        var asset = CreateAsset();
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);
        _employeeRepository.Setup(x => x.GetByIdAsync(777)).ReturnsAsync((Employee?)null);

        var service = CreateService();
        var request = CreateRequest(asset.Id, asset.DepartmentId);
        request.EmployeeId = 777;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request, "Admin IT", 1));

        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmployeeInactive_ThrowsArgumentException()
    {
        var asset = CreateAsset();
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);
        _employeeRepository.Setup(x => x.GetByIdAsync(100))
            .ReturnsAsync(new Employee { Id = 100, FullName = "Da Nghi Viec", DepartmentId = 1, IsActive = false });

        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, asset.DepartmentId), "Admin IT", 1));

        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Never);
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

        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Never);
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
    public async Task CreateAsync_ManagerTargetsOtherDepartment_ThrowsDepartmentForbiddenException()
    {
        // Tài sản thuộc đúng phòng ban Manager, nhưng chọn phòng ban NHẬN khác phòng ban mình.
        var asset = CreateAsset(departmentId: 1);
        _assetRepository.Setup(x => x.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);

        var service = CreateService();

        await Assert.ThrowsAsync<DepartmentForbiddenException>(() =>
            service.CreateAsync(CreateRequest(asset.Id, departmentId: 2), "Manager", 1));
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
            1,
            currentUserId: 7);

        Assert.Equal(50, result.Id);
        Assert.Equal(asset.Id, result.AssetId);
        Assert.Equal("Nguyen Van A", result.RecipientName);
        Assert.Equal(100, created!.EmployeeId);
        Assert.Equal(asset.DepartmentId, created.DepartmentId); // phòng ban nhận = phòng ban của nhân viên.
        Assert.Equal(7, created.HandedOverByUserId);            // Bên giao = người thực hiện phân bổ.
        Assert.Equal("Tốt", created.HandoverCondition);
        Assert.Equal("Allocated", result.Status);
        Assert.Null(result.ReturnedDate);
        Assert.Null(result.ReturnCondition);
        _allocationRepository.Verify(x => x.AddAsync(It.IsAny<AssetAllocation>()), Times.Once);
    }

    // ----- ReturnAsync (UC-15) -----

    [Fact]
    public async Task ReturnAsync_AllocationNotFound_ThrowsAllocationNotFoundException()
    {
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(99)).ReturnsAsync((AssetAllocation?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<AllocationNotFoundException>(() =>
            service.ReturnAsync(99, CreateReturnRequest("Good"), "Admin IT", null));
    }

    [Fact]
    public async Task ReturnAsync_ManagerOutsideDepartment_ThrowsDepartmentForbiddenException()
    {
        var asset = CreateAsset(departmentId: 2);
        var allocation = CreateAllocation(assetId: asset.Id, departmentId: asset.DepartmentId);
        allocation.Asset = asset;
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);

        var service = CreateService();

        await Assert.ThrowsAsync<DepartmentForbiddenException>(() =>
            service.ReturnAsync(allocation.Id, CreateReturnRequest("Good"), "Manager", 1));

        _allocationRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_AllocationAlreadyReturned_ThrowsAllocationAlreadyReturnedException()
    {
        var allocation = CreateAllocation(status: AllocationStatus.Returned);
        allocation.Asset = CreateAsset();
        allocation.ReturnedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);

        var service = CreateService();

        await Assert.ThrowsAsync<AllocationAlreadyReturnedException>(() =>
            service.ReturnAsync(allocation.Id, CreateReturnRequest("Good"), "Admin IT", 1));
    }

    [Fact]
    public async Task ReturnAsync_ReturnedDateBeforeAllocatedDate_ThrowsArgumentException()
    {
        var allocation = CreateAllocation();
        allocation.Asset = CreateAsset();
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);

        var service = CreateService();
        var request = CreateReturnRequest("Good");
        request.ReturnedDate = allocation.AllocatedDate.AddDays(-1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReturnAsync(allocation.Id, request, "Admin IT", 1));

        _allocationRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_ConditionGood_DoesNotChangeAssetStatus()
    {
        var asset = CreateAsset();
        var allocation = CreateAllocation(assetId: asset.Id, departmentId: asset.DepartmentId);
        allocation.Asset = asset; // Include(Asset) — GetEntityByIdAsync tracked kèm navigation.
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
        // Chỉ 1 lần SaveChangesAsync duy nhất, qua allocationRepository — không có lần lưu asset riêng.
        _allocationRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        _assetRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_ConditionDamaged_SetsAssetStatusToBroken()
    {
        var asset = CreateAsset();
        var allocation = CreateAllocation(assetId: asset.Id, departmentId: asset.DepartmentId);
        allocation.Asset = asset; // Include(Asset) — GetEntityByIdAsync tracked kèm navigation.
        _allocationRepository.Setup(x => x.GetEntityByIdAsync(allocation.Id)).ReturnsAsync(allocation);
        _allocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _allocationRepository
            .Setup(x => x.GetByIdWithDetailsAsync(allocation.Id))
            .Returns(() => Task.FromResult<AssetAllocation?>(WithNavigation(allocation, asset)));

        var service = CreateService();
        var result = await service.ReturnAsync(
            allocation.Id,
            CreateReturnRequest("Damaged"),
            "Admin IT",
            1);

        Assert.Equal(AssetStatus.Broken, asset.Status);
        Assert.Equal("Damaged", result.ReturnCondition);
        _assetRepository.Verify(x => x.Update(asset), Times.Once);
        // Regression: trước đây có 2 lần SaveChangesAsync riêng biệt (allocation rồi asset), vi phạm quy
        // tắc UC-15 "phải nằm trong cùng 1 transaction" — giờ chỉ còn đúng 1 lần, không hề gọi qua
        // _assetRepository (asset được ghi cùng allocation trong 1 SaveChangesAsync của _allocationRepository).
        _allocationRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        _assetRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    // ----- GetPagedAsync / GetByIdAsync -----

    [Fact]
    public async Task GetPagedAsync_Manager_IgnoresRequestedDepartmentAndUsesOwn()
    {
        _allocationRepository
            .Setup(x => x.GetPagedAsync(It.IsAny<int?>(), It.IsAny<AllocationStatus?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<AssetAllocation>(), 0));

        var service = CreateService();
        await service.GetPagedAsync(departmentId: 2, status: null, page: 1, pageSize: 20, "Manager", currentUserDepartmentId: 1);

        _allocationRepository.Verify(x => x.GetPagedAsync(1, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidStatus_ThrowsArgumentException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPagedAsync(null, "Lost", 1, 20, "Admin IT", null));
    }

    [Fact]
    public async Task GetByIdAsync_ManagerOutsideDepartment_ThrowsAllocationNotFoundException()
    {
        var allocation = CreateAllocation(departmentId: 2);
        _allocationRepository
            .Setup(x => x.GetByIdWithDetailsAsync(allocation.Id))
            .ReturnsAsync(WithNavigation(allocation, CreateAsset(departmentId: 2)));

        var service = CreateService();

        await Assert.ThrowsAsync<AllocationNotFoundException>(() =>
            service.GetByIdAsync(allocation.Id, "Manager", 1));
    }

    // ----- GetOverdueAsync -----

    [Fact]
    public async Task GetOverdueAsync_UsesLastMaintenanceDate_WhenPresent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _allocationRepository
            .Setup(x => x.GetOpenAllocationCandidatesAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<AllocationOverdueCandidateRow>
            {
                new()
                {
                    AllocationId = 1,
                    AssetId = 10,
                    AssetCode = "TS-001",
                    AssetName = "Laptop",
                    DepartmentName = "Phong IT",
                    RecipientName = "A",
                    AllocatedDate = today.AddDays(-400),
                    AssetPurchaseDate = today.AddDays(-1000),
                    LastMaintenanceDate = today.AddDays(-200).ToDateTime(TimeOnly.MinValue),
                },
            });

        var service = CreateService();
        var result = await service.GetOverdueAsync(180, "Admin IT", null);

        var item = Assert.Single(result);
        Assert.Equal(200, item.DaysSinceLastMaintenance);
        Assert.Equal(today.AddDays(-200), item.LastMaintenanceDate);
    }

    [Fact]
    public async Task GetOverdueAsync_NoMaintenanceHistory_FallsBackToPurchaseDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _allocationRepository
            .Setup(x => x.GetOpenAllocationCandidatesAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<AllocationOverdueCandidateRow>
            {
                new()
                {
                    AllocationId = 1,
                    AssetId = 10,
                    AssetCode = "TS-001",
                    AssetName = "Laptop",
                    DepartmentName = "Phong IT",
                    RecipientName = "A",
                    AllocatedDate = today.AddDays(-50),
                    AssetPurchaseDate = today.AddDays(-300),
                    LastMaintenanceDate = null,
                },
            });

        var service = CreateService();
        var result = await service.GetOverdueAsync(180, "Admin IT", null);

        var item = Assert.Single(result);
        Assert.Equal(300, item.DaysSinceLastMaintenance);
        Assert.Null(item.LastMaintenanceDate);
    }

    [Fact]
    public async Task GetOverdueAsync_WithinThreshold_IsExcluded()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _allocationRepository
            .Setup(x => x.GetOpenAllocationCandidatesAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<AllocationOverdueCandidateRow>
            {
                new()
                {
                    AllocationId = 1,
                    AssetId = 10,
                    AssetCode = "TS-001",
                    AssetName = "Laptop",
                    DepartmentName = "Phong IT",
                    RecipientName = "A",
                    AllocatedDate = today.AddDays(-10),
                    AssetPurchaseDate = today.AddDays(-10),
                    LastMaintenanceDate = today.AddDays(-10).ToDateTime(TimeOnly.MinValue),
                },
            });

        var service = CreateService();
        var result = await service.GetOverdueAsync(180, "Admin IT", null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOverdueAsync_Manager_IsScopedToOwnDepartment()
    {
        _allocationRepository
            .Setup(x => x.GetOpenAllocationCandidatesAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<AllocationOverdueCandidateRow>());

        var service = CreateService();
        await service.GetOverdueAsync(180, "Manager", 3);

        _allocationRepository.Verify(x => x.GetOpenAllocationCandidatesAsync(3), Times.Once);
    }

    [Fact]
    public async Task GetOverdueAsync_ThresholdOutOfRange_IsClamped()
    {
        _allocationRepository
            .Setup(x => x.GetOpenAllocationCandidatesAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<AllocationOverdueCandidateRow>());

        var service = CreateService();

        // Không throw dù truyền giá trị âm/quá lớn — Math.Clamp(1, 3650) xử lý êm, không phải lỗi 400.
        await service.GetOverdueAsync(-5, "Admin IT", null);
        await service.GetOverdueAsync(999999, "Admin IT", null);
    }

    [Fact]
    public async Task GetPagedAsync_KeywordAndEmployeeFilter_ArePassedToRepository()
    {
        _allocationRepository
            .Setup(x => x.GetPagedAsync(It.IsAny<int?>(), It.IsAny<AllocationStatus?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<AssetAllocation>(), 0));

        var service = CreateService();
        await service.GetPagedAsync(null, null, 1, 20, "Admin IT", null, keyword: "an", employeeId: 5);

        _allocationRepository.Verify(x => x.GetPagedAsync(null, null, "an", 5, 1, 20), Times.Once);
    }

    // ----- GetDocumentAsync (biên bản theo mẫu) -----

    private AssetAllocation DocumentAllocation(bool returned = false)
    {
        var asset = CreateAsset();
        var allocation = WithNavigation(CreateAllocation(), asset);
        allocation.EmployeeId = 100;
        allocation.Employee = new Employee
        {
            Id = 100, FullName = "Nguyễn Văn An", Position = "Kế toán", DepartmentId = 1,
            Department = new Department { Id = 1, Name = "Phòng Kế toán" },
        };
        allocation.HandoverReason = "Cấp phát phục vụ công việc";
        allocation.HandoverLocation = "Văn phòng";
        allocation.HandoverCondition = "Mới";
        allocation.HandedOverBy = new User
        {
            Id = 7, FullName = "Quản trị IT", Role = new Role { Id = 1, Name = "Admin IT" },
            Department = new Department { Id = 9, Name = "Phòng IT" },
        };
        if (returned)
        {
            allocation.ReturnedDate = allocation.AllocatedDate.AddDays(5);
            allocation.ReturnCondition = AssetReturnCondition.Damaged;
            allocation.ReturnNote = "Nghỉ việc";
            allocation.ReceivedBy = allocation.HandedOverBy;
        }

        _allocationRepository.Setup(x => x.GetByIdWithDetailsAsync(allocation.Id)).ReturnsAsync(allocation);
        return allocation;
    }

    [Fact]
    public async Task GetDocumentAsync_Handover_GiverIsCompanyUserAndReceiverIsEmployee()
    {
        var allocation = DocumentAllocation();
        _allocationRepository.Setup(x => x.GetReferencePriceAsync(1)).ReturnsAsync(15_000_000m);

        var doc = await CreateService().GetDocumentAsync(allocation.Id, null, "Admin IT", null);

        Assert.Equal("Handover", doc.Kind);
        Assert.Equal("Quản trị IT", doc.Giver.FullName);
        Assert.Equal("Admin IT", doc.Giver.Position);
        Assert.Equal("Phòng IT", doc.Giver.DepartmentName);
        Assert.Equal("Nguyễn Văn An", doc.Receiver.FullName);
        Assert.Equal("Kế toán", doc.Receiver.Position);
        Assert.Equal("Phòng Kế toán", doc.Receiver.DepartmentName);
        Assert.Equal("Mới", doc.Asset.Condition);
        Assert.Equal(15_000_000m, doc.Asset.Amount);
        Assert.Equal("Văn phòng", doc.Location);
        Assert.Equal(allocation.AllocatedDate, doc.DocumentDate);
    }

    [Fact]
    public async Task GetDocumentAsync_NoReferencePrice_AmountIsNull()
    {
        var allocation = DocumentAllocation();
        _allocationRepository.Setup(x => x.GetReferencePriceAsync(It.IsAny<int>())).ReturnsAsync((decimal?)null);

        var doc = await CreateService().GetDocumentAsync(allocation.Id, "handover", "Admin IT", null);

        Assert.Null(doc.Asset.Amount);
    }

    [Fact]
    public async Task GetDocumentAsync_Return_PartiesAreSwapped()
    {
        var allocation = DocumentAllocation(returned: true);

        var doc = await CreateService().GetDocumentAsync(allocation.Id, "return", "Admin IT", null);

        Assert.Equal("Return", doc.Kind);
        Assert.Equal("Nguyễn Văn An", doc.Giver.FullName);   // nhân viên trả lại
        Assert.Equal("Quản trị IT", doc.Receiver.FullName);  // công ty nhận lại
        Assert.Equal("Hỏng", doc.Asset.Condition);
        Assert.Equal(allocation.ReturnedDate, doc.DocumentDate);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnKindBeforeReturned_ThrowsArgumentException()
    {
        var allocation = DocumentAllocation();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetDocumentAsync(allocation.Id, "return", "Admin IT", null));
    }

    [Fact]
    public async Task GetDocumentAsync_UnknownKind_ThrowsArgumentException()
    {
        var allocation = DocumentAllocation();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().GetDocumentAsync(allocation.Id, "xyz", "Admin IT", null));
    }

    [Fact]
    public async Task GetDocumentAsync_ManagerOtherDepartment_ThrowsNotFound()
    {
        var allocation = DocumentAllocation(); // phòng ban 1

        await Assert.ThrowsAsync<AllocationNotFoundException>(
            () => CreateService().GetDocumentAsync(allocation.Id, null, "Manager", 2));
    }

    private AssetAllocationService CreateService() => new(
        _allocationRepository.Object,
        _assetRepository.Object,
        _employeeRepository.Object,
        _exclusive,
        NullLogger<AssetAllocationService>.Instance);

    private static CreateAssetAllocationDto CreateRequest(int assetId, int departmentId) => new()
    {
        AssetId = assetId,
        EmployeeId = departmentId * 100,
        AllocatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        HandoverReason = "Cấp phát phục vụ công việc",
        HandoverLocation = "Văn phòng",
        HandoverCondition = "Tốt",
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
