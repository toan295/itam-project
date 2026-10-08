using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITAM.Tests.Services;

public class AssetServiceTests
{
    private readonly Mock<IAssetRepository> _repoMock = new();
    private readonly Mock<IMaintenanceTicketRepository> _ticketRepoMock = new();
    private readonly AssetService _sut;

    public AssetServiceTests()
    {
        _sut = new AssetService(_repoMock.Object, _ticketRepoMock.Object);

        // Mặc định: CategoryId/DepartmentId hợp lệ, chưa có AssetCode nào trùng — từng test override khi cần.
        _repoMock.Setup(r => r.CategoryExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.DepartmentExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.GetByAssetCodeAsync(It.IsAny<string>())).ReturnsAsync((Asset?)null);
    }

    private static CreateAssetRequestDto ValidCreateDto(string assetCode = "TS-001", int departmentId = 1) => new()
    {
        AssetCode = assetCode,
        Name = "Laptop Dell",
        CategoryId = 1,
        DepartmentId = departmentId,
    };

    private static UpdateAssetRequestDto ValidUpdateDto(
        string assetCode = "TS-001", int departmentId = 1, string status = "InUse") => new()
    {
        AssetCode = assetCode,
        Name = "Laptop Dell",
        CategoryId = 1,
        DepartmentId = departmentId,
        Status = status,
    };

    private void SetupAddAsyncReturnsCreatedAsset()
    {
        Asset? savedAsset = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Asset>()))
            .Callback<Asset>(a => { a.Id = 1; savedAsset = a; })
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(() => savedAsset);
    }

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_DuplicateAssetCode_ThrowsAndDoesNotAdd()
    {
        _repoMock.Setup(r => r.GetByAssetCodeAsync("TS-001"))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001" });

        await Assert.ThrowsAsync<AssetCodeAlreadyExistsException>(
            () => _sut.CreateAsync(ValidCreateDto(), "Admin IT", 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Asset>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NewAssetCode_DefaultsStatusToInUse()
    {
        SetupAddAsyncReturnsCreatedAsset();

        var result = await _sut.CreateAsync(ValidCreateDto(), "Admin IT", 1);

        Assert.Equal("InUse", result.Status);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TrimsAssetCodeAndName()
    {
        SetupAddAsyncReturnsCreatedAsset();
        var dto = ValidCreateDto();
        dto.AssetCode = "  TS-777  ";
        dto.Name = "  Laptop HP  ";

        var result = await _sut.CreateAsync(dto, "Admin IT", 1);

        Assert.Equal("TS-777", result.AssetCode);
        Assert.Equal("Laptop HP", result.Name);
    }

    [Fact]
    public async Task CreateAsync_CategoryDoesNotExist_ThrowsArgumentExceptionAndDoesNotAdd()
    {
        _repoMock.Setup(r => r.CategoryExistsAsync(It.IsAny<int>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(ValidCreateDto(), "Admin IT", 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Asset>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DepartmentDoesNotExist_ThrowsArgumentException()
    {
        _repoMock.Setup(r => r.DepartmentExistsAsync(It.IsAny<int>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(ValidCreateDto(), "Admin IT", 1));
    }

    [Fact]
    public async Task CreateAsync_ManagerOutsideOwnDepartment_ThrowsDepartmentForbiddenAndDoesNotAdd()
    {
        var dto = ValidCreateDto(departmentId: 2);

        await Assert.ThrowsAsync<DepartmentForbiddenException>(
            () => _sut.CreateAsync(dto, currentUserRole: "Manager", currentUserDepartmentId: 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Asset>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ManagerInsideOwnDepartment_Succeeds()
    {
        SetupAddAsyncReturnsCreatedAsset();
        var dto = ValidCreateDto(departmentId: 1);

        var result = await _sut.CreateAsync(dto, currentUserRole: "Manager", currentUserDepartmentId: 1);

        Assert.Equal(1, result.DepartmentId);
    }

    [Fact]
    public async Task CreateAsync_SaveChangesThrowsDbUpdateException_MapsToAssetCodeAlreadyExists()
    {
        // Race hiếm: 2 request cùng lúc vượt qua bước kiểm tra trùng mã ở tầng Service — unique index
        // DB là chốt chặn cuối, phải ánh xạ về đúng 409 thân thiện thay vì để lỗi 500 lộ ra ngoài.
        _repoMock.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new DbUpdateException("duplicate key"));

        await Assert.ThrowsAsync<AssetCodeAlreadyExistsException>(() => _sut.CreateAsync(ValidCreateDto(), "Admin IT", 1));
    }

    // ----- UpdateAsync -----

    [Fact]
    public async Task UpdateAsync_AssetNotFound_ThrowsAssetNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.UpdateAsync(999, ValidUpdateDto(), "Admin IT", 1));
    }

    [Fact]
    public async Task UpdateAsync_ManagerOutsideAssetCurrentDepartment_ThrowsDepartmentForbidden()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 2 });

        await Assert.ThrowsAsync<DepartmentForbiddenException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(departmentId: 2), currentUserRole: "Manager", currentUserDepartmentId: 1));
    }

    [Fact]
    public async Task UpdateAsync_ManagerMovingAssetToAnotherDepartment_ThrowsDepartmentForbidden()
    {
        // Asset hiện đang ở phòng ban của Manager (1), nhưng dto muốn chuyển sang phòng khác (2) -> vẫn phải chặn.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });

        await Assert.ThrowsAsync<DepartmentForbiddenException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(departmentId: 2), currentUserRole: "Manager", currentUserDepartmentId: 1));
    }

    [Fact]
    public async Task UpdateAsync_InvalidStatus_ThrowsArgumentException()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(status: "KhongTonTai"), "Admin IT", 1));
    }

    [Fact]
    public async Task UpdateAsync_NumericStatusString_ThrowsArgumentException()
    {
        // Enum.TryParse mặc định chấp nhận chuỗi số ("3" -> Disposed) — phải bị từ chối vì Status
        // trong DTO chỉ được phép là tên trạng thái (xem UpdateAssetRequestDto.Status).
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(status: "3"), "Admin IT", 1));
    }

    [Fact]
    public async Task UpdateAsync_ManagerSetsStatusDisposed_ThrowsAssetDisposalNotAllowed()
    {
        // Không ai được lách qua Update để đặt Disposed — phải dùng phiếu thanh lý.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = AssetStatus.InUse });

        await Assert.ThrowsAsync<AssetDisposalNotAllowedException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(status: "Disposed"), currentUserRole: "Manager", currentUserDepartmentId: 1));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_AdminSetsStatusDisposed_ThrowsAssetDisposalNotAllowed()
    {
        // Thanh lý bắt buộc qua phiếu thanh lý — kể cả Admin IT cũng không được đặt Disposed trực tiếp.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = AssetStatus.InUse });

        await Assert.ThrowsAsync<AssetDisposalNotAllowedException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(status: "Disposed"), "Admin IT", 1));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ManagerEditsAlreadyDisposedAsset_DoesNotThrow()
    {
        // Asset đã Disposed từ trước, Manager chỉ sửa các trường khác (status trong dto giữ nguyên
        // "Disposed") -> không phải là một hành động chuyển trạng thái, không nên bị chặn.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = AssetStatus.Disposed });

        var result = await _sut.UpdateAsync(1, ValidUpdateDto(status: "Disposed"), "Manager", 1);

        Assert.Equal("Disposed", result.Status);
    }

    [Fact]
    public async Task UpdateAsync_AssetCodeUnchanged_DoesNotCheckDuplicate()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });

        await _sut.UpdateAsync(1, ValidUpdateDto(assetCode: "TS-001"), "Admin IT", 1);

        _repoMock.Verify(r => r.GetByAssetCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_AssetCodeChangedToDuplicate_ThrowsAssetCodeAlreadyExists()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });
        _repoMock.Setup(r => r.GetByAssetCodeAsync("TS-999"))
            .ReturnsAsync(new Asset { Id = 2, AssetCode = "TS-999" });

        await Assert.ThrowsAsync<AssetCodeAlreadyExistsException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(assetCode: "TS-999"), "Admin IT", 1));
    }

    [Fact]
    public async Task UpdateAsync_AssetCodeChangedToUniqueValue_Succeeds()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });
        _repoMock.Setup(r => r.GetByAssetCodeAsync("TS-NEW")).ReturnsAsync((Asset?)null);

        var result = await _sut.UpdateAsync(1, ValidUpdateDto(assetCode: "TS-NEW"), "Admin IT", 1);

        Assert.Equal("TS-NEW", result.AssetCode);
    }

    // ----- Số serial không được trùng -----

    [Fact]
    public async Task CreateAsync_DuplicateSerialNumber_ThrowsAndDoesNotAdd()
    {
        _repoMock.Setup(r => r.SerialNumberExistsAsync("SN-1", null)).ReturnsAsync(true);
        var dto = ValidCreateDto();
        dto.SerialNumber = "SN-1";

        await Assert.ThrowsAsync<AssetSerialNumberAlreadyExistsException>(() => _sut.CreateAsync(dto, "Admin IT", 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Asset>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankSerialNumber_IsStoredAsNullAndNeverChecked(string? serial)
    {
        SetupAddAsyncReturnsCreatedAsset();
        var dto = ValidCreateDto();
        dto.SerialNumber = serial;

        await _sut.CreateAsync(dto, "Admin IT", 1);

        _repoMock.Verify(r => r.SerialNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
        _repoMock.Verify(r => r.AddAsync(It.Is<Asset>(a => a.SerialNumber == null)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SerialNumberIsTrimmedBeforeCheckAndStore()
    {
        SetupAddAsyncReturnsCreatedAsset();
        var dto = ValidCreateDto();
        dto.SerialNumber = "  SN-9  ";

        await _sut.CreateAsync(dto, "Admin IT", 1);

        _repoMock.Verify(r => r.SerialNumberExistsAsync("SN-9", null), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.Is<Asset>(a => a.SerialNumber == "SN-9")), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SerialChangedToOneUsedElsewhere_Throws()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, SerialNumber = "OLD" });
        _repoMock.Setup(r => r.SerialNumberExistsAsync("TAKEN", 1)).ReturnsAsync(true);
        var dto = ValidUpdateDto();
        dto.SerialNumber = "TAKEN";

        await Assert.ThrowsAsync<AssetSerialNumberAlreadyExistsException>(() => _sut.UpdateAsync(1, dto, "Admin IT", 1));
    }

    [Fact]
    public async Task UpdateAsync_SerialUnchanged_DoesNotCheckSoLegacyDuplicatesDoNotBlockOtherEdits()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, SerialNumber = "DUP" });
        _repoMock.Setup(r => r.SerialNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(true);
        var dto = ValidUpdateDto();
        dto.SerialNumber = "dup";   // chỉ khác hoa/thường -> không coi là đổi serial.

        var result = await _sut.UpdateAsync(1, dto, "Admin IT", 1);

        Assert.Equal("TS-001", result.AssetCode);
        _repoMock.Verify(r => r.SerialNumberExistsAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ManagerReactivatesDisposedAsset_ThrowsReactivationNotAllowed()
    {
        // Manager không được "hồi sinh" tài sản Admin IT đã thanh lý qua PUT.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = AssetStatus.Disposed });

        await Assert.ThrowsAsync<AssetReactivationNotAllowedException>(
            () => _sut.UpdateAsync(1, ValidUpdateDto(status: "InUse"), "Manager", 1));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_AdminReactivatesDisposedAsset_Succeeds()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = AssetStatus.Disposed });

        var result = await _sut.UpdateAsync(1, ValidUpdateDto(status: "InUse"), "Admin IT", 1);

        Assert.Equal("InUse", result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianOtherDepartment_ReturnsAsset()
    {
        // Technician phục vụ mọi phòng ban — không bị giới hạn theo phòng ban như Manager.
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 2 });

        var result = await _sut.GetByIdAsync(1, "Technician", 1, 7);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task SearchAsync_Technician_IsNotScopedToDepartment()
    {
        _repoMock.Setup(r => r.SearchAsync(null, 7, null, null, null, null, null, 1, 20))
            .ReturnsAsync((new List<Asset>(), 0));

        await _sut.SearchAsync(new AssetSearchFilterDto { DepartmentId = 7 }, "Technician", 3, 9);

        _repoMock.Verify(r => r.SearchAsync(null, 7, null, null, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerOtherDepartment_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 2 });

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.GetByIdAsync(1, "Manager", 1, 7));
    }


    // ----- GetByIdAsync / IsUnderWarranty -----

    [Fact]
    public async Task GetByIdAsync_WarrantyInFuture_IsUnderWarrantyTrue()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(new Asset
        {
            Id = 1,
            AssetCode = "TS-001",
            WarrantyExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        });

        var result = await _sut.GetByIdAsync(1, "Admin IT", null, null);

        Assert.True(result.IsUnderWarranty);
    }

    [Fact]
    public async Task GetByIdAsync_WarrantyInPast_IsUnderWarrantyFalse()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(new Asset
        {
            Id = 1,
            AssetCode = "TS-001",
            WarrantyExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
        });

        var result = await _sut.GetByIdAsync(1, "Admin IT", null, null);

        Assert.False(result.IsUnderWarranty);
    }

    [Fact]
    public async Task GetByIdAsync_NoWarrantyExpiry_IsUnderWarrantyFalse()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", WarrantyExpiry = null });

        var result = await _sut.GetByIdAsync(1, "Admin IT", null, null);

        Assert.False(result.IsUnderWarranty);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsAssetNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.GetByIdAsync(999, "Admin IT", null, null));
    }

    [Fact]
    public async Task GetByIdAsync_ManagerOutsideDepartment_ThrowsAssetNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 2 });

        // Không lộ 403 (mới biết asset tồn tại) — phải là 404 y như khi asset thật sự không tồn tại.
        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.GetByIdAsync(1, "Manager", 1, null));
    }

    [Fact]
    public async Task GetByIdAsync_ManagerInsideDepartment_ReturnsAsset()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1 });

        var result = await _sut.GetByIdAsync(1, "Manager", 1, null);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_AdminOutsideAnyDepartment_ReturnsAsset()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 2 });

        var result = await _sut.GetByIdAsync(1, "Admin IT", 1, null);

        Assert.Equal(1, result.Id);
    }

    // ----- GetPagedAsync: UC-08 E2 giới hạn theo phòng ban -----

    [Fact]
    public async Task GetPagedAsync_InvalidStatus_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetPagedAsync(null, "KhongTonTai", 1, 20, "Admin IT", 1, null));
    }

    [Fact]
    public async Task GetPagedAsync_AdminIT_UsesRequestedDepartmentId()
    {
        _repoMock.Setup(r => r.GetPagedAsync(5, null, null, 1, 20)).ReturnsAsync((new List<Asset>(), 0));

        await _sut.GetPagedAsync(5, null, 1, 20, "Admin IT", 1, null);

        _repoMock.Verify(r => r.GetPagedAsync(5, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Manager_IgnoresRequestedDepartmentId_UsesOwnDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(1, null, null, 1, 20)).ReturnsAsync((new List<Asset>(), 0));

        // Manager gửi departmentId=5 (phòng khác) nhưng phải bị ghi đè bằng phòng ban của chính họ (1).
        await _sut.GetPagedAsync(5, null, 1, 20, "Manager", 1, null);

        _repoMock.Verify(r => r.GetPagedAsync(1, null, null, 1, 20), Times.Once);
        _repoMock.Verify(r => r.GetPagedAsync(5, null, null, 1, 20), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_ManagerWithMissingDepartmentClaim_UsesSentinelToReturnNothing()
    {
        _repoMock.Setup(r => r.GetPagedAsync(-1, null, null, 1, 20)).ReturnsAsync((new List<Asset>(), 0));

        // Claim DepartmentId thiếu/hỏng -> phải chặn hẳn (sentinel -1) thay vì mặc định mở toàn bộ dữ liệu.
        await _sut.GetPagedAsync(null, null, 1, 20, "Manager", null, null);

        _repoMock.Verify(r => r.GetPagedAsync(-1, null, null, 1, 20), Times.Once);
    }

    // ----- SearchAsync -----

    [Fact]
    public async Task SearchAsync_InvalidWarrantyStatus_ThrowsArgumentException()
    {
        var filter = new AssetSearchFilterDto { WarrantyStatus = "KhongHopLe" };

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(filter, "Admin IT", 1, null));
    }

    [Fact]
    public async Task SearchAsync_WarrantyStatusValid_MapsToIsUnderWarrantyTrue()
    {
        _repoMock.Setup(r => r.SearchAsync(null, null, null, null, null, true, null, 1, 20))
            .ReturnsAsync((new List<Asset>(), 0));

        await _sut.SearchAsync(new AssetSearchFilterDto { WarrantyStatus = "Valid" }, "Admin IT", 1, null);

        _repoMock.Verify(r => r.SearchAsync(null, null, null, null, null, true, null, 1, 20), Times.Once);
    }

    // ----- Đổi trạng thái tay -> đồng bộ phiếu bảo trì -----

    private void SetupExistingAsset(AssetStatus status) =>
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(new Asset { Id = 1, AssetCode = "TS-001", DepartmentId = 1, Status = status });

    [Fact]
    public async Task UpdateAsync_InUseToMaintenance_CreatesPendingTicket()
    {
        SetupExistingAsset(AssetStatus.InUse);
        _ticketRepoMock.Setup(r => r.HasPendingTicketAsync(1)).ReturnsAsync(false);

        await _sut.UpdateAsync(1, ValidUpdateDto(status: "Maintenance"), "Admin IT", 1);

        _ticketRepoMock.Verify(r => r.AddAsync(It.Is<MaintenanceTicket>(t =>
            t.AssetId == 1 && t.Status == TicketStatus.Pending && t.Priority == TicketPriority.Normal)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ToMaintenance_WhenPendingTicketAlreadyExists_DoesNotCreateAnother()
    {
        SetupExistingAsset(AssetStatus.InUse);
        _ticketRepoMock.Setup(r => r.HasPendingTicketAsync(1)).ReturnsAsync(true);

        await _sut.UpdateAsync(1, ValidUpdateDto(status: "Maintenance"), "Admin IT", 1);

        _ticketRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Theory]
    [InlineData("InUse", TicketStatus.Resolved)]
    [InlineData("Broken", TicketStatus.Failed)]
    public async Task UpdateAsync_LeavingMaintenance_ClosesPendingTickets(string newStatus, TicketStatus expected)
    {
        SetupExistingAsset(AssetStatus.Maintenance);
        var pending = new MaintenanceTicket { Id = 9, AssetId = 1, Status = TicketStatus.Pending };
        _ticketRepoMock.Setup(r => r.GetPendingByAssetAsync(1)).ReturnsAsync(new List<MaintenanceTicket> { pending });

        await _sut.UpdateAsync(1, ValidUpdateDto(status: newStatus), "Admin IT", 1);

        Assert.Equal(expected, pending.Status);
        Assert.NotNull(pending.ResolvedDate);
    }

    [Fact]
    public async Task UpdateAsync_StatusUnchanged_DoesNotTouchTickets()
    {
        SetupExistingAsset(AssetStatus.InUse);

        await _sut.UpdateAsync(1, ValidUpdateDto(status: "InUse"), "Admin IT", 1);

        _ticketRepoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
        _ticketRepoMock.Verify(r => r.GetPendingByAssetAsync(It.IsAny<int>()), Times.Never);
    }
}
