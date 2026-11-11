using ITAM.API.Models.DTOs.MaintenanceTickets;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Repositories.Models;
using ITAM.API.Services.Implementations;
using Moq;

namespace ITAM.Tests.Services;

public class MaintenanceTicketServiceTests
{
    private const int DeptIt = 1;
    private const int DeptHr = 2;
    private const int TechnicianUserId = 10;
    private const int OtherTechnicianUserId = 11;

    private readonly Mock<IMaintenanceTicketRepository> _repoMock = new();
    private readonly Mock<IAssetRepository> _assetRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly MaintenanceTicketService _sut;

    public MaintenanceTicketServiceTests()
    {
        _sut = new MaintenanceTicketService(_repoMock.Object, _assetRepoMock.Object, _employeeRepoMock.Object);

        // Mặc định: mọi TechnicianId đều hợp lệ — từng test override khi cần.
        _repoMock.Setup(r => r.TechnicianExistsAsync(It.IsAny<int>())).ReturnsAsync(true);
    }

    private static Asset MakeAsset(int id = 1, int departmentId = DeptIt, AssetStatus status = AssetStatus.InUse) => new()
    {
        Id = id,
        AssetCode = $"TS-{id:000}",
        Name = "Laptop Dell",
        DepartmentId = departmentId,
        Status = status,
    };

    private static MaintenanceTicket MakeTicket(
        int id = 1, TicketStatus status = TicketStatus.Pending, int? technicianId = null,
        Asset? asset = null) => new()
    {
        Id = id,
        AssetId = (asset ?? MakeAsset()).Id,
        Asset = asset ?? MakeAsset(),
        IssueDescription = "Không lên nguồn",
        Status = status,
        TechnicianId = technicianId,
        ReportedDate = DateTime.UtcNow.AddHours(-5),
    };

    private void SetupAsset(Asset asset) =>
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(asset.Id)).ReturnsAsync(asset);

    private void SetupTicket(MaintenanceTicket ticket) =>
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(ticket.Id)).ReturnsAsync(ticket);

    private void SetupAddAsyncReturnsCreatedTicket(Asset asset)
    {
        MaintenanceTicket? saved = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceTicket>()))
            .Callback<MaintenanceTicket>(t => { t.Id = 100; t.Asset = asset; saved = t; })
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(100)).ReturnsAsync(() => saved);
    }

    private static CreateMaintenanceTicketRequestDto CreateDto(int assetId = 1, int? technicianId = null) => new()
    {
        AssetId = assetId,
        IssueDescription = "  Màn hình nhấp nháy  ",
        TechnicianId = technicianId,
    };

    // ----- CreateAsync (UC-11) -----

    [Fact]
    public async Task CreateAsync_ValidAsset_CreatesPendingTicketWithTrimmedDescription()
    {
        var asset = MakeAsset();
        SetupAsset(asset);
        SetupAddAsyncReturnsCreatedTicket(asset);

        var result = await _sut.CreateAsync(CreateDto(), "Admin IT", null, 1);

        Assert.Equal("Pending", result.Status);
        Assert.Equal("Màn hình nhấp nháy", result.IssueDescription);
        Assert.Null(result.ResolvedDate);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_AssetNotFound_ThrowsAssetNotFoundAndDoesNotAdd()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.CreateAsync(CreateDto(99), "Admin IT", null, 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Theory]
    [InlineData(AssetStatus.Maintenance)]
    [InlineData(AssetStatus.Disposed)]
    public async Task CreateAsync_AssetNotInUseOrBroken_ThrowsNotEligible(AssetStatus status)
    {
        SetupAsset(MakeAsset(status: status));

        await Assert.ThrowsAsync<AssetNotEligibleForMaintenanceException>(
            () => _sut.CreateAsync(CreateDto(), "Admin IT", null, 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_BrokenAsset_IsAllowed()
    {
        var asset = MakeAsset(status: AssetStatus.Broken);
        SetupAsset(asset);
        SetupAddAsyncReturnsCreatedTicket(asset);

        var result = await _sut.CreateAsync(CreateDto(), "Admin IT", null, 1);

        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task CreateAsync_ManagerAssetInOtherDepartment_ThrowsAssetNotFound()
    {
        SetupAsset(MakeAsset(departmentId: DeptHr));

        await Assert.ThrowsAsync<AssetNotFoundException>(
            () => _sut.CreateAsync(CreateDto(), "Manager", DeptIt, 2));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Technician_IsForbidden()
    {
        // Kỹ thuật viên không tạo phiếu — phiếu do người dùng được cấp tài sản báo, Admin IT phân công.
        SetupAsset(MakeAsset());

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(
            () => _sut.CreateAsync(CreateDto(technicianId: OtherTechnicianUserId), "Technician", DeptIt, TechnicianUserId));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TechnicianIdInvalid_ThrowsArgumentException()
    {
        SetupAsset(MakeAsset());
        _repoMock.Setup(r => r.TechnicianExistsAsync(50)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateAsync(CreateDto(technicianId: 50), "Admin IT", null, 1));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    // ----- AssignTechnicianAsync (UC-11 bước 5, UC-12 bước 1) -----

    [Fact]
    public async Task AssignTechnicianAsync_AdminReassignsAnyTechnician_Succeeds()
    {
        var ticket = MakeTicket(technicianId: OtherTechnicianUserId);
        SetupTicket(ticket);

        await _sut.AssignTechnicianAsync(1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId, Reason = "Cân bằng khối lượng" }, "Admin IT", null, 1);

        Assert.Equal(TechnicianUserId, ticket.TechnicianId);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AssignTechnicianAsync_ManagerAssignsInvalidTechnician_ThrowsArgumentException()
    {
        SetupTicket(MakeTicket());
        _repoMock.Setup(r => r.TechnicianExistsAsync(50)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = 50 }, "Manager", DeptIt, 2));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignTechnicianAsync_ManagerOutsideDepartment_ThrowsNotFound()
    {
        SetupTicket(MakeTicket(asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Manager", DeptIt, 2));
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianCannotSelfClaim_ThrowsNotFoundForUnassignedTicket()
    {
        // Phiếu chưa phân công nằm ngoài phạm vi của Technician -> 404; không có đường "tự nhận".
        var ticket = MakeTicket();
        SetupTicket(ticket);

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Technician", DeptIt, TechnicianUserId));

        Assert.Null(ticket.TechnicianId);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianOnOwnTicket_ThrowsForbidden()
    {
        SetupTicket(MakeTicket(technicianId: TechnicianUserId));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = OtherTechnicianUserId }, "Technician", DeptIt, TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianClaimsAlreadyAssignedTicket_ThrowsAlreadyAssigned()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId));

        // Phiếu của kỹ thuật viên khác nằm ngoài phạm vi -> 404 (không lộ có người đã nhận).
        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Technician", DeptIt, TechnicianUserId));
    }

    [Fact]
    public async Task AssignTechnicianAsync_ClosedTicket_ThrowsTransitionNotAllowed()
    {
        SetupTicket(MakeTicket(status: TicketStatus.Resolved));

        await Assert.ThrowsAsync<TicketStatusTransitionNotAllowedException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Admin IT", null, 1));
    }

    [Fact]
    public async Task AssignTechnicianAsync_TicketNotFound_Throws()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((MaintenanceTicket?)null);

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.AssignTechnicianAsync(
            99, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Admin IT", null, 1));
    }

    // ----- UpdateStatusAsync (UC-12) -----

    [Fact]
    public async Task UpdateStatusAsync_AssignedTechnicianResolves_SetsResolvedAndAssetInUse()
    {
        var asset = MakeAsset(status: AssetStatus.Broken);
        var ticket = MakeTicket(technicianId: TechnicianUserId, asset: asset);
        SetupTicket(ticket);

        var result = await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "  Đã thay nguồn  " }, "Technician", TechnicianUserId);

        Assert.Equal("Resolved", result.Status);
        Assert.Equal("Đã thay nguồn", result.Notes);
        Assert.NotNull(result.ResolvedDate);
        Assert.Equal(AssetStatus.InUse, asset.Status);
        _assetRepoMock.Verify(r => r.Update(asset), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once); // 1 lần duy nhất cho cả ticket + asset.
    }

    [Fact]
    public async Task UpdateStatusAsync_Failed_SetsAssetBroken()
    {
        var asset = MakeAsset(status: AssetStatus.InUse);
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: asset));

        var result = await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Failed", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId);

        Assert.Equal("Failed", result.Status);
        Assert.Equal(AssetStatus.Broken, asset.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_AdminCanUpdateTicketOfAnyTechnician()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId));

        var result = await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Admin IT", 1);

        Assert.Equal("Resolved", result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_OtherTechnician_ThrowsForbiddenAndDoesNotSave()
    {
        var asset = MakeAsset();
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId, asset: asset));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnassignedTicketByTechnician_ThrowsForbidden()
    {
        SetupTicket(MakeTicket(technicianId: null));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Failed)]
    public async Task UpdateStatusAsync_ClosedTicket_ThrowsTransitionNotAllowedAndDoesNotSave(TicketStatus current)
    {
        SetupTicket(MakeTicket(status: current, technicianId: TechnicianUserId));

        await Assert.ThrowsAsync<TicketStatusTransitionNotAllowedException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("1")]
    [InlineData("resolved")]
    public async Task UpdateStatusAsync_InvalidTargetStatus_ThrowsArgumentException(string status)
    {
        SetupTicket(MakeTicket(technicianId: TechnicianUserId));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = status, Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_TicketNotFound_Throws()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((MaintenanceTicket?)null);

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.UpdateStatusAsync(
            99, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Admin IT", 1));
    }

    // ----- GetByIdAsync / GetPagedAsync -----

    [Fact]
    public async Task GetByIdAsync_ManagerOutsideDepartment_ThrowsNotFound()
    {
        SetupTicket(MakeTicket(asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(
            () => _sut.GetByIdAsync(1, "Manager", DeptIt, null));
    }

    [Fact]
    public async Task GetByIdAsync_ClosedTicket_ComputesResolutionHours()
    {
        var ticket = MakeTicket(status: TicketStatus.Resolved);
        ticket.ReportedDate = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        ticket.ResolvedDate = new DateTime(2026, 1, 1, 14, 30, 0, DateTimeKind.Utc);
        SetupTicket(ticket);

        var result = await _sut.GetByIdAsync(1, "Admin IT", null, null);

        Assert.Equal(6.5, result.ResolutionHours);
    }

    [Fact]
    public async Task GetPagedAsync_Manager_IgnoresRequestedDepartmentAndUsesOwn()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(DeptHr, null, null, null, null, 1, 20, "Manager", DeptIt, null);

        _repoMock.Verify(r => r.GetPagedAsync(
            DeptIt, null, null, null, null, null, null, null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_ManagerWithoutDepartmentClaim_QueriesSentinelDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Manager", null, null);

        _repoMock.Verify(r => r.GetPagedAsync(-1, null, null, null, null, null, null, null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidStatus_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetPagedAsync(null, null, "2", null, null, 1, 20, "Admin IT", null, null));
    }

    [Fact]
    public async Task GetPagedAsync_FromAfterTo_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPagedAsync(
            null, null, null, new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 1), 1, 20, "Admin IT", null, null));
    }

    [Fact]
    public async Task GetPagedAsync_ToDateIsInclusive_PassesNextDayAsExclusiveUpperBound()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(
            null, null, null, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), 1, 20, "Admin IT", null, null);

        _repoMock.Verify(r => r.GetPagedAsync(
            null, null, null, null, null, new DateTime(2026, 5, 1), new DateTime(2026, 6, 1), null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_OutOfRangePageSize_FallsBackToDefault()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        var result = await _sut.GetPagedAsync(null, null, null, null, null, 0, 500, "Admin IT", null, null);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    // ----- Regression: lỗi phát hiện khi rà soát A1/A2 -----

    [Theory]
    [InlineData("Resolved")]
    [InlineData("Failed")]
    public async Task UpdateStatusAsync_AssetAlreadyDisposed_ClosesTicketButKeepsAssetDisposed(string status)
    {
        var asset = MakeAsset(status: AssetStatus.Disposed);
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: asset));

        var result = await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = status, Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId);

        Assert.Equal(status, result.Status);
        Assert.Equal(AssetStatus.Disposed, asset.Status); // không bị "hồi sinh" thành InUse/Broken.
        _assetRepoMock.Verify(r => r.Update(It.IsAny<Asset>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianOwnTicketOfAnyDepartment_CanRead()
    {
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: MakeAsset(departmentId: DeptHr)));

        var result = await _sut.GetByIdAsync(1, "Technician", DeptIt, TechnicianUserId);

        Assert.Equal(TechnicianUserId, result.TechnicianId);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianUnassignedTicket_ThrowsNotFound()
    {
        // Chưa phân công thì chưa phải việc của kỹ thuật viên nào.
        SetupTicket(MakeTicket(technicianId: null, asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(
            () => _sut.GetByIdAsync(1, "Technician", DeptIt, TechnicianUserId));
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianOtherTechniciansTicket_ThrowsNotFound()
    {
        // Không để Technician xem phiếu (và tên) của kỹ thuật viên khác.
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId, asset: MakeAsset(departmentId: DeptIt)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(
            () => _sut.GetByIdAsync(1, "Technician", DeptIt, TechnicianUserId));
    }

    [Fact]
    public async Task GetByIdAsync_ManagerNeverGetsAssignedExemption()
    {
        // Ngoại lệ "được gán cho mình" chỉ dành cho Technician, không áp dụng cho Manager.
        SetupTicket(MakeTicket(technicianId: 5, asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(
            () => _sut.GetByIdAsync(1, "Manager", DeptIt, 5));
    }

    [Fact]
    public async Task GetPagedAsync_Technician_IsScopedToAssignedTickets()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Technician", DeptIt, TechnicianUserId);

        _repoMock.Verify(r => r.GetPagedAsync(null, null, null, null, null, null, null, TechnicianUserId, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Admin IT")]
    public async Task GetPagedAsync_NonTechnician_DoesNotPassAssignedTechnicianFilter(string role)
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, role, DeptIt, 7);

        _repoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<int?>(), null, null, null, null, null, null, null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianOnOwnTicketOfOtherDepartment_ThrowsForbidden()
    {
        // Phiếu (khác phòng ban) đã phân công cho chính họ: thấy được nhưng không có quyền phân công lại.
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Technician", DeptIt, TechnicianUserId));
    }

    // ----- GetStatsAsync (UC-13) -----

    private static MaintenanceTicketStatRow Row(
        int assetId, string code, TicketStatus status, DateTime reported, double? hoursToResolve = null) => new()
    {
        AssetId = assetId,
        AssetCode = code,
        Status = status,
        ReportedDate = reported,
        ResolvedDate = hoursToResolve.HasValue ? reported.AddHours(hoursToResolve.Value) : null,
    };

    private void SetupStatRows(params MaintenanceTicketStatRow[] rows) =>
        _repoMock.Setup(r => r.GetStatRowsAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(rows.ToList());

    [Fact]
    public async Task GetStatsAsync_NoData_ReturnsZerosAndEmptyNotError()
    {
        SetupStatRows();

        var stats = await _sut.GetStatsAsync(null, null, null, null, "Admin IT", null);

        Assert.Equal(0, stats.CountByStatus["Pending"]);
        Assert.Equal(0, stats.CountByStatus["Resolved"]);
        Assert.Equal(0, stats.CountByStatus["Failed"]);
        Assert.Null(stats.AverageResolutionHours);
        Assert.Empty(stats.ByAssetPerYear);
    }

    [Fact]
    public async Task GetStatsAsync_CountsByStatusAndAveragesOnlyClosedTickets()
    {
        var t = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        SetupStatRows(
            Row(1, "TS-001", TicketStatus.Pending, t),
            Row(1, "TS-001", TicketStatus.Resolved, t, 2),
            Row(2, "TS-002", TicketStatus.Failed, t, 4));

        var stats = await _sut.GetStatsAsync(null, null, null, null, "Admin IT", null);

        Assert.Equal(1, stats.CountByStatus["Pending"]);
        Assert.Equal(1, stats.CountByStatus["Resolved"]);
        Assert.Equal(1, stats.CountByStatus["Failed"]);
        Assert.Equal(3.0, stats.AverageResolutionHours); // (2 + 4) / 2 — phiếu Pending không tính.
    }

    [Fact]
    public async Task GetStatsAsync_GroupsByAssetAndYearOrdered()
    {
        SetupStatRows(
            Row(2, "TS-002", TicketStatus.Pending, new DateTime(2026, 1, 5)),
            Row(1, "TS-001", TicketStatus.Pending, new DateTime(2026, 2, 5)),
            Row(1, "TS-001", TicketStatus.Pending, new DateTime(2026, 6, 5)),
            Row(1, "TS-001", TicketStatus.Pending, new DateTime(2025, 12, 31)));

        var stats = await _sut.GetStatsAsync(null, null, null, null, "Admin IT", null);

        Assert.Collection(stats.ByAssetPerYear,
            x => { Assert.Equal("TS-001", x.AssetCode); Assert.Equal(2025, x.Year); Assert.Equal(1, x.TicketCount); },
            x => { Assert.Equal("TS-001", x.AssetCode); Assert.Equal(2026, x.Year); Assert.Equal(2, x.TicketCount); },
            x => { Assert.Equal("TS-002", x.AssetCode); Assert.Equal(2026, x.Year); Assert.Equal(1, x.TicketCount); });
    }

    [Fact]
    public async Task GetStatsAsync_Manager_IsScopedToOwnDepartment()
    {
        SetupStatRows();

        await _sut.GetStatsAsync(DeptHr, null, null, null, "Manager", DeptIt);

        _repoMock.Verify(r => r.GetStatRowsAsync(DeptIt, null, null, null), Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_FromAfterTo_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetStatsAsync(
            null, null, new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 1), "Admin IT", null));
    }

    // ----- Mức độ khẩn -----

    [Fact]
    public async Task CreateAsync_WithoutPriority_DefaultsToNormal()
    {
        SetupAsset(MakeAsset());
        MaintenanceTicket? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceTicket>())).Callback<MaintenanceTicket>(t => { t.Id = 1; added = t; }).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(() => added);

        var result = await _sut.CreateAsync(CreateDto(), "Admin IT", null, 1);

        Assert.Equal("Normal", result.Priority);
    }

    [Fact]
    public async Task CreateAsync_WithUrgentPriority_StoresUrgent()
    {
        SetupAsset(MakeAsset());
        MaintenanceTicket? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceTicket>())).Callback<MaintenanceTicket>(t => { t.Id = 1; added = t; }).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(() => added);
        var dto = CreateDto();
        dto.Priority = "Urgent";

        var result = await _sut.CreateAsync(dto, "Admin IT", null, 1);

        Assert.Equal("Urgent", result.Priority);
        Assert.Equal(TicketPriority.Urgent, added!.Priority);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("urgent")]
    [InlineData("Critical")]
    public async Task CreateAsync_InvalidPriority_ThrowsArgumentException(string priority)
    {
        SetupAsset(MakeAsset());
        var dto = CreateDto();
        dto.Priority = priority;

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(dto, "Admin IT", null, 1));
    }

    [Fact]
    public async Task UpdatePriorityAsync_PendingTicket_ChangesPriority()
    {
        var ticket = MakeTicket();
        SetupTicket(ticket);

        var result = await _sut.UpdatePriorityAsync(1, new UpdateTicketPriorityRequestDto { Priority = "High" }, "Admin IT", null, 1);

        Assert.Equal("High", result.Priority);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePriorityAsync_ClosedTicket_ThrowsTransitionNotAllowed()
    {
        SetupTicket(MakeTicket(status: TicketStatus.Resolved));

        await Assert.ThrowsAsync<TicketStatusTransitionNotAllowedException>(
            () => _sut.UpdatePriorityAsync(1, new UpdateTicketPriorityRequestDto { Priority = "High" }, "Admin IT", null, 1));
    }

    [Fact]
    public async Task UpdatePriorityAsync_ManagerOutsideDepartment_ThrowsNotFound()
    {
        SetupTicket(MakeTicket(asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(
            () => _sut.UpdatePriorityAsync(1, new UpdateTicketPriorityRequestDto { Priority = "Urgent" }, "Manager", DeptIt, 2));
    }

    [Fact]
    public async Task GetPagedAsync_PriorityFilter_IsParsedAndPassedToRepository()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Admin IT", null, null, "Urgent");

        _repoMock.Verify(r => r.GetPagedAsync(null, null, null, TicketPriority.Urgent, null, null, null, null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_InvalidPriorityFilter_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Admin IT", null, null, "Nope"));
    }

    // ----- Tự đồng bộ trạng thái tài sản -----

    [Theory]
    [InlineData(AssetStatus.InUse)]
    [InlineData(AssetStatus.Broken)]
    public async Task CreateAsync_SetsAssetStatusToMaintenance(AssetStatus before)
    {
        var asset = MakeAsset(status: before);
        SetupAsset(asset);
        SetupAddAsyncReturnsCreatedTicket(asset);

        await _sut.CreateAsync(CreateDto(), "Admin IT", null, 1);

        Assert.Equal(AssetStatus.Maintenance, asset.Status);
        _assetRepoMock.Verify(r => r.Update(asset), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once); // ticket + asset cùng 1 lần lưu.
    }

    [Fact]
    public async Task UpdateStatusAsync_Resolved_WhenAnotherPendingTicketExists_KeepsAssetInMaintenance()
    {
        var asset = MakeAsset(status: AssetStatus.Maintenance);
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: asset));
        _repoMock.Setup(r => r.HasOtherPendingTicketsAsync(asset.Id, 1)).ReturnsAsync(true);

        await _sut.UpdateStatusAsync(1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId);

        Assert.Equal(AssetStatus.Maintenance, asset.Status);
        _assetRepoMock.Verify(r => r.Update(It.IsAny<Asset>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_Resolved_NoOtherPending_AssetBackToInUse()
    {
        var asset = MakeAsset(status: AssetStatus.Maintenance);
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: asset));

        await _sut.UpdateStatusAsync(1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã kiểm tra xong" }, "Technician", TechnicianUserId);

        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    [Fact]
    public async Task GetPagedAsync_AdvancedFilters_AreMappedToRepositoryExtras()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), 1, 20, It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Admin IT", null, null,
            technicianId: 7, categoryId: 3, pendingOverDays: 2, sort: "oldest");

        _repoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), 1, 20, It.IsAny<string?>(),
            It.Is<TicketListExtras>(x => x.TechnicianId == 7 && x.CategoryId == 3 && x.Sort == "oldest"
                && x.PendingBefore != null && x.PendingBefore < DateTime.UtcNow.AddDays(-1.9) && x.PendingBefore > DateTime.UtcNow.AddDays(-2.1))), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_TechnicianAndUnassignedTogether_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPagedAsync(
            null, null, null, null, null, 1, 20, "Admin IT", null, null, technicianId: 7, unassigned: true));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(3651, null)]
    [InlineData(null, "random")]
    public async Task GetPagedAsync_InvalidAdvancedValues_ThrowArgumentException(int? pendingOverDays, string? sort)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetPagedAsync(
            null, null, null, null, null, 1, 20, "Admin IT", null, null, pendingOverDays: pendingOverDays, sort: sort));
    }

    [Fact]
    public async Task GetPagedAsync_ClosedFilter_IsPassedToRepository()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketPriority?>(), It.IsAny<bool?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<TicketListExtras>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Admin IT", null, null, null, closed: true);

        _repoMock.Verify(r => r.GetPagedAsync(null, null, null, null, true, null, null, null, 1, 20, null, It.IsAny<TicketListExtras>()), Times.Once);
    }

    // ----- Nhân viên (vai trò Employee) báo lỗi tài sản được cấp -----

    private const int EmployeeUserId = 77;
    private const int EmployeeId = 5;

    private void SetupEmployee(bool active = true, bool holdsAsset = true)
    {
        _employeeRepoMock.Setup(r => r.GetByUserIdAsync(EmployeeUserId))
            .ReturnsAsync(new Employee { Id = EmployeeId, UserId = EmployeeUserId, IsActive = active, DepartmentId = DeptIt });
        _employeeRepoMock.Setup(r => r.HasOpenAllocationForAssetAsync(EmployeeId, 1)).ReturnsAsync(holdsAsset);
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(MakeAsset());
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => MakeTicket(id));
    }

    [Fact]
    public async Task CreateAsync_EmployeeReportingTheirOwnAsset_SetsReporterAndIgnoresTechnicianId()
    {
        SetupEmployee();
        MaintenanceTicket? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceTicket>())).Callback<MaintenanceTicket>(t => added = t).Returns(Task.CompletedTask);

        await _sut.CreateAsync(
            new CreateMaintenanceTicketRequestDto { AssetId = 1, IssueDescription = "Màn hình nhấp nháy", TechnicianId = 10 },
            "Employee", DeptIt, EmployeeUserId);

        Assert.Equal(EmployeeUserId, added!.ReportedByUserId);
        Assert.Null(added.TechnicianId);                       // nhân viên không tự chọn người xử lý
        Assert.Equal(TicketStatus.Pending, added.Status);
    }

    [Fact]
    public async Task CreateAsync_EmployeeReportingAnAssetTheyDoNotHold_IsNotFound()
    {
        SetupEmployee(holdsAsset: false);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.CreateAsync(
            new CreateMaintenanceTicketRequestDto { AssetId = 1, IssueDescription = "x" }, "Employee", DeptIt, EmployeeUserId));
        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmployeeAccountWithoutLinkedEmployee_IsNotFound()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(MakeAsset());
        _employeeRepoMock.Setup(r => r.GetByUserIdAsync(EmployeeUserId)).ReturnsAsync((Employee?)null);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.CreateAsync(
            new CreateMaintenanceTicketRequestDto { AssetId = 1, IssueDescription = "x" }, "Employee", DeptIt, EmployeeUserId));
    }

    [Fact]
    public async Task CreateAsync_InactiveEmployee_IsNotFound()
    {
        SetupEmployee(active: false);

        await Assert.ThrowsAsync<AssetNotFoundException>(() => _sut.CreateAsync(
            new CreateMaintenanceTicketRequestDto { AssetId = 1, IssueDescription = "x" }, "Employee", DeptIt, EmployeeUserId));
    }

    [Fact]
    public async Task CreateAsync_StaffTicketAlsoRecordsWhoReportedIt()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(MakeAsset());
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((int id) => MakeTicket(id));
        MaintenanceTicket? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<MaintenanceTicket>())).Callback<MaintenanceTicket>(t => added = t).Returns(Task.CompletedTask);

        await _sut.CreateAsync(new CreateMaintenanceTicketRequestDto { AssetId = 1, IssueDescription = "x" }, "Admin IT", null, 3);

        Assert.Equal(3, added!.ReportedByUserId);
        _employeeRepoMock.Verify(r => r.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
    }

    // ===== Kết quả bắt buộc, phiên bản, lịch sử =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ok")]
    public async Task UpdateStatusAsync_MissingOrTooShortResult_ThrowsAndKeepsTicketPending(string? notes)
    {
        var ticket = MakeTicket(technicianId: TechnicianUserId);
        SetupTicket(ticket);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = notes }, "Technician", TechnicianUserId));

        Assert.Equal(TicketStatus.Pending, ticket.Status);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_StaleVersion_ThrowsConflictAndDoesNotSave()
    {
        var ticket = MakeTicket(technicianId: TechnicianUserId);
        ticket.Version = 3;
        SetupTicket(ticket);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã thay nguồn", Version = 2 }, "Technician", TechnicianUserId));

        Assert.Equal(TicketStatus.Pending, ticket.Status);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignTechnicianAsync_StaleVersion_ThrowsConflict()
    {
        var ticket = MakeTicket();
        ticket.Version = 5;
        SetupTicket(ticket);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId, Version = 4 }, "Admin IT", null, 1));

        Assert.Null(ticket.TechnicianId);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignTechnicianAsync_ReassignWithoutReason_Throws()
    {
        var ticket = MakeTicket(technicianId: OtherTechnicianUserId);
        SetupTicket(ticket);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Admin IT", null, 1));

        Assert.Equal(OtherTechnicianUserId, ticket.TechnicianId);
    }

    [Fact]
    public async Task AssignTechnicianAsync_SameTechnician_Throws()
    {
        SetupTicket(MakeTicket(technicianId: TechnicianUserId));

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId, Reason = "x" }, "Admin IT", null, 1));
    }

    [Fact]
    public async Task AssignTechnicianAsync_Reassign_RecordsHistoryWithOldNewAndReason()
    {
        var ticket = MakeTicket(technicianId: OtherTechnicianUserId);
        ticket.Technician = new User { Id = OtherTechnicianUserId, FullName = "KTV Cũ" };
        SetupTicket(ticket);
        _repoMock.Setup(r => r.GetUserFullNameAsync(TechnicianUserId)).ReturnsAsync("KTV Mới");
        _repoMock.Setup(r => r.GetUserFullNameAsync(1)).ReturnsAsync("Admin");

        await _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId, Reason = "KTV cũ nghỉ phép" }, "Admin IT", null, 1);

        var entry = Assert.Single(ticket.History);
        Assert.Equal("Reassigned", entry.Action);
        Assert.Equal("KTV Cũ", entry.FromValue);
        Assert.Equal("KTV Mới", entry.ToValue);
        Assert.Equal("KTV cũ nghỉ phép", entry.Reason);
        Assert.Equal("Admin", entry.ActorName);
    }

    [Fact]
    public async Task UpdateStatusAsync_Resolved_RecordsHistoryWithResult()
    {
        var ticket = MakeTicket(technicianId: TechnicianUserId);
        SetupTicket(ticket);

        await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved", Notes = "Đã thay nguồn" }, "Technician", TechnicianUserId);

        var entry = Assert.Single(ticket.History);
        Assert.Equal("Resolved", entry.Action);
        Assert.Equal("Pending", entry.FromValue);
        Assert.Equal("Đã thay nguồn", entry.Reason);
        Assert.Equal(TechnicianUserId, entry.ActorUserId);
    }

    [Fact]
    public async Task UpdatePriorityAsync_Unchanged_DoesNotAddHistory()
    {
        var ticket = MakeTicket();
        ticket.Priority = TicketPriority.High;
        SetupTicket(ticket);

        await _sut.UpdatePriorityAsync(1, new UpdateTicketPriorityRequestDto { Priority = "High" }, "Admin IT", null, 1);

        Assert.Empty(ticket.History);
    }

    [Fact]
    public async Task UpdatePriorityAsync_Changed_RecordsHistory()
    {
        var ticket = MakeTicket();
        SetupTicket(ticket);

        await _sut.UpdatePriorityAsync(1, new UpdateTicketPriorityRequestDto { Priority = "Urgent" }, "Admin IT", null, 1);

        var entry = Assert.Single(ticket.History);
        Assert.Equal("PriorityChanged", entry.Action);
        Assert.Equal("Urgent", entry.ToValue);
    }

    [Fact]
    public async Task GetHistoryAsync_OutOfScopeTechnician_ThrowsNotFound()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId));

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.GetHistoryAsync(1, "Technician", null, TechnicianUserId));
    }
}
