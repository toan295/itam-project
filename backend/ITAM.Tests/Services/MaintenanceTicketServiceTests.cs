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
    private readonly MaintenanceTicketService _sut;

    public MaintenanceTicketServiceTests()
    {
        _sut = new MaintenanceTicketService(_repoMock.Object, _assetRepoMock.Object);

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

    [Theory]
    [InlineData("Manager")]
    [InlineData("Technician")]
    public async Task CreateAsync_AssetInOtherDepartment_ThrowsAssetNotFound(string role)
    {
        SetupAsset(MakeAsset(departmentId: DeptHr));

        await Assert.ThrowsAsync<AssetNotFoundException>(
            () => _sut.CreateAsync(CreateDto(), role, DeptIt, TechnicianUserId));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<MaintenanceTicket>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TechnicianAssignsAnotherTechnician_ThrowsForbidden()
    {
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

        await _sut.AssignTechnicianAsync(1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Admin IT", null, 1);

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
    public async Task AssignTechnicianAsync_TechnicianSelfClaimsUnassignedTicket_Succeeds()
    {
        var ticket = MakeTicket();
        SetupTicket(ticket);

        await _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = TechnicianUserId }, "Technician", DeptIt, TechnicianUserId);

        Assert.Equal(TechnicianUserId, ticket.TechnicianId);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianAssignsSomeoneElse_ThrowsForbidden()
    {
        SetupTicket(MakeTicket());

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.AssignTechnicianAsync(
            1, new AssignTechnicianRequestDto { TechnicianId = OtherTechnicianUserId }, "Technician", DeptIt, TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianClaimsAlreadyAssignedTicket_ThrowsAlreadyAssigned()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId));

        await Assert.ThrowsAsync<TicketAlreadyAssignedException>(() => _sut.AssignTechnicianAsync(
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
            1, new UpdateTicketStatusRequestDto { Status = "Failed" }, "Technician", TechnicianUserId);

        Assert.Equal("Failed", result.Status);
        Assert.Equal(AssetStatus.Broken, asset.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_AdminCanUpdateTicketOfAnyTechnician()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId));

        var result = await _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved" }, "Admin IT", 1);

        Assert.Equal("Resolved", result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_OtherTechnician_ThrowsForbiddenAndDoesNotSave()
    {
        var asset = MakeAsset();
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId, asset: asset));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved" }, "Technician", TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnassignedTicketByTechnician_ThrowsForbidden()
    {
        SetupTicket(MakeTicket(technicianId: null));

        await Assert.ThrowsAsync<TicketAssignmentForbiddenException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved" }, "Technician", TechnicianUserId));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Failed)]
    public async Task UpdateStatusAsync_ClosedTicket_ThrowsTransitionNotAllowedAndDoesNotSave(TicketStatus current)
    {
        SetupTicket(MakeTicket(status: current, technicianId: TechnicianUserId));

        await Assert.ThrowsAsync<TicketStatusTransitionNotAllowedException>(() => _sut.UpdateStatusAsync(
            1, new UpdateTicketStatusRequestDto { Status = "Resolved" }, "Technician", TechnicianUserId));

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
            1, new UpdateTicketStatusRequestDto { Status = status }, "Technician", TechnicianUserId));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_TicketNotFound_Throws()
    {
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((MaintenanceTicket?)null);

        await Assert.ThrowsAsync<MaintenanceTicketNotFoundException>(() => _sut.UpdateStatusAsync(
            99, new UpdateTicketStatusRequestDto { Status = "Resolved" }, "Admin IT", 1));
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
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(DeptHr, null, null, null, null, 1, 20, "Manager", DeptIt, null);

        _repoMock.Verify(r => r.GetPagedAsync(
            DeptIt, null, null, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_ManagerWithoutDepartmentClaim_QueriesSentinelDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Manager", null, null);

        _repoMock.Verify(r => r.GetPagedAsync(-1, null, null, null, null, null, 1, 20), Times.Once);
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
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(
            null, null, null, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), 1, 20, "Admin IT", null, null);

        _repoMock.Verify(r => r.GetPagedAsync(
            null, null, null, new DateTime(2026, 5, 1), new DateTime(2026, 6, 1), null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_OutOfRangePageSize_FallsBackToDefault()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
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
            1, new UpdateTicketStatusRequestDto { Status = status }, "Technician", TechnicianUserId);

        Assert.Equal(status, result.Status);
        Assert.Equal(AssetStatus.Disposed, asset.Status); // không bị "hồi sinh" thành InUse/Broken.
        _assetRepoMock.Verify(r => r.Update(It.IsAny<Asset>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianAssignedToTicketOfOtherDepartment_CanRead()
    {
        // Admin/Manager có thể gán kỹ thuật viên khác phòng ban — người đó vẫn phải thấy phiếu của mình.
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: MakeAsset(departmentId: DeptHr)));

        var result = await _sut.GetByIdAsync(1, "Technician", DeptIt, TechnicianUserId);

        Assert.Equal(TechnicianUserId, result.TechnicianId);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianNotAssignedAndOtherDepartment_ThrowsNotFound()
    {
        SetupTicket(MakeTicket(technicianId: OtherTechnicianUserId, asset: MakeAsset(departmentId: DeptHr)));

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
    public async Task GetPagedAsync_Technician_AlsoRequestsTicketsAssignedToThem()
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, "Technician", DeptIt, TechnicianUserId);

        _repoMock.Verify(r => r.GetPagedAsync(DeptIt, null, null, null, null, TechnicianUserId, 1, 20), Times.Once);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Admin IT")]
    public async Task GetPagedAsync_NonTechnician_DoesNotPassAssignedTechnicianFilter(string role)
    {
        _repoMock.Setup(r => r.GetPagedAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<TicketStatus?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((new List<MaintenanceTicket>(), 0));

        await _sut.GetPagedAsync(null, null, null, null, null, 1, 20, role, DeptIt, 7);

        _repoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<int?>(), null, null, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task AssignTechnicianAsync_TechnicianAlreadyAssignedOtherDepartment_CannotReassignToSelfAgain()
    {
        // Phiếu (khác phòng ban) đã gán cho chính họ: vẫn thấy được nhưng "tự nhận" lần nữa -> 409.
        SetupTicket(MakeTicket(technicianId: TechnicianUserId, asset: MakeAsset(departmentId: DeptHr)));

        await Assert.ThrowsAsync<TicketAlreadyAssignedException>(() => _sut.AssignTechnicianAsync(
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
}
