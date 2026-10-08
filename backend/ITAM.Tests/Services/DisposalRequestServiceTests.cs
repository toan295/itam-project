using ITAM.Tests.Helpers;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Moq;

namespace ITAM.Tests.Services;

public class DisposalRequestServiceTests
{
    private const int DeptIt = 1;
    private const int DeptHr = 2;

    private readonly Mock<IDisposalRequestRepository> _repoMock = new();
    private readonly Mock<IDisposalStatusRepository> _statusRepoMock = new();
    private readonly Mock<IAssetRepository> _assetRepoMock = new();
    private readonly Mock<IAssetAllocationRepository> _allocationRepoMock = new();
    private readonly Mock<IAuditLogService> _auditMock = new();
    private readonly Mock<IMaintenanceTicketRepository> _ticketRepoMock = new();
    private readonly RecordingExclusiveSection _exclusive = new();
    private readonly DisposalRequestService _sut;

    private static readonly Dictionary<string, DisposalStatus> Statuses = new()
    {
        [DisposalStatusCodes.Inspected] = new() { Id = 1, Code = DisposalStatusCodes.Inspected, Name = "Đã kiểm tra", Color = "info", IsSystem = true },
        [DisposalStatusCodes.Proposed] = new() { Id = 2, Code = DisposalStatusCodes.Proposed, Name = "Đã đề xuất", Color = "warning", IsSystem = true },
        [DisposalStatusCodes.Approved] = new() { Id = 3, Code = DisposalStatusCodes.Approved, Name = "Đã duyệt", Color = "success", IsSystem = true },
        [DisposalStatusCodes.Rejected] = new() { Id = 4, Code = DisposalStatusCodes.Rejected, Name = "Từ chối", Color = "danger", IsSystem = true },
        [DisposalStatusCodes.Completed] = new() { Id = 5, Code = DisposalStatusCodes.Completed, Name = "Hoàn tất", Color = "slate", IsSystem = true },
    };

    public DisposalRequestServiceTests()
    {
        _sut = new DisposalRequestService(
            _repoMock.Object, _statusRepoMock.Object, _assetRepoMock.Object, _allocationRepoMock.Object, _ticketRepoMock.Object, _auditMock.Object, _exclusive);

        foreach (var (code, status) in Statuses)
        {
            _statusRepoMock.Setup(r => r.GetByCodeAsync(code)).ReturnsAsync(status);
        }

        _auditMock.Setup(a => a.RecordAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<object?>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        _allocationRepoMock.Setup(r => r.HasOpenAllocationAsync(It.IsAny<int>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.HasOpenRequestForAssetAsync(It.IsAny<int>())).ReturnsAsync(false);
        _ticketRepoMock.Setup(r => r.GetPendingByAssetAsync(It.IsAny<int>())).ReturnsAsync(new List<MaintenanceTicket>());
    }

    private static DisposalRequest MakeRequest(string statusCode, int departmentId = DeptIt, AssetStatus assetStatus = AssetStatus.Broken) => new()
    {
        Id = 1,
        AssetId = 7,
        StatusId = Statuses[statusCode].Id,
        Status = Statuses[statusCode],
        InspectionNote = "Hỏng nặng",
        InspectedByUserId = 5,
        InspectedBy = new User { Id = 5, FullName = "KTV A" },
        Asset = new Asset
        {
            Id = 7, AssetCode = "PC-007", Name = "May", DepartmentId = departmentId,
            Department = new Department { Id = departmentId, Name = "Phong" }, Status = assetStatus,
        },
    };

    private void SetupRequest(DisposalRequest request) =>
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(request.Id)).ReturnsAsync(request);

    // ----- CreateAsync (bước 1: kiểm tra) -----

    [Fact]
    public async Task CreateAsync_ValidAsset_CreatesInspectedRequest()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7))
            .ReturnsAsync(new Asset { Id = 7, AssetCode = "PC-007", Status = AssetStatus.Broken });
        DisposalRequest? added = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<DisposalRequest>()))
            .Callback<DisposalRequest>(r => { r.Id = 1; added = r; })
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(() => MakeRequest(DisposalStatusCodes.Inspected));

        var result = await _sut.CreateAsync(new CreateDisposalRequestDto { AssetId = 7, InspectionNote = "  Hỏng nặng  " }, currentUserId: 5);

        Assert.Equal(DisposalStatusCodes.Inspected, result.StatusCode);
        Assert.Equal("Hỏng nặng", added!.InspectionNote);
        Assert.Equal(5, added.InspectedByUserId);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Exactly(2)); // Id sinh tự động -> lưu 2 lần.
    }

    [Fact]
    public async Task CreateAsync_AssetNotFound_Throws()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((Asset?)null);

        await Assert.ThrowsAsync<AssetNotFoundException>(
            () => _sut.CreateAsync(new CreateDisposalRequestDto { AssetId = 99, InspectionNote = "x" }, 5));
    }

    [Fact]
    public async Task CreateAsync_AssetAlreadyDisposed_ThrowsConflict()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(new Asset { Id = 7, Status = AssetStatus.Disposed });

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.CreateAsync(new CreateDisposalRequestDto { AssetId = 7, InspectionNote = "x" }, 5));
    }

    [Fact]
    public async Task CreateAsync_AssetAlreadyHasOpenRequest_ThrowsConflict()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7)).ReturnsAsync(new Asset { Id = 7, Status = AssetStatus.Broken });
        _repoMock.Setup(r => r.HasOpenRequestForAssetAsync(7)).ReturnsAsync(true);

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.CreateAsync(new CreateDisposalRequestDto { AssetId = 7, InspectionNote = "x" }, 5));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<DisposalRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_LocksTheAssetRowToSerializeConcurrentRequests()
    {
        _assetRepoMock.Setup(r => r.GetByIdWithDetailsAsync(7))
            .ReturnsAsync(new Asset { Id = 7, AssetCode = "PC-007", Status = AssetStatus.Broken });
        _repoMock.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync(MakeRequest(DisposalStatusCodes.Inspected));

        await _sut.CreateAsync(new CreateDisposalRequestDto { AssetId = 7, InspectionNote = "x" }, 5);

        Assert.Equal((LockTarget.Asset, 7), Assert.Single(_exclusive.Calls));
    }

    [Fact]
    public async Task CompleteAsync_LocksTheAssetRowSoNoAllocationCanSlipIn()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Approved));

        await _sut.CompleteAsync(1, new CompleteDisposalRequestDto(), 9);

        Assert.Equal((LockTarget.Asset, 7), Assert.Single(_exclusive.Calls));
    }

    // ----- ProposeAsync (bước 2: đề xuất) -----

    [Fact]
    public async Task ProposeAsync_InspectedRequest_MovesToProposed()
    {
        var request = MakeRequest(DisposalStatusCodes.Inspected);
        SetupRequest(request);

        var result = await _sut.ProposeAsync(1, new ProposeDisposalRequestDto { Reason = " Hết khấu hao ", DisposalMethod = "Bán phế liệu" }, 5);

        Assert.Equal(DisposalStatusCodes.Proposed, result.StatusCode);
        Assert.Equal("Hết khấu hao", request.Reason);
        Assert.Equal(5, request.ProposedByUserId);
        Assert.NotNull(request.ProposedAt);
    }

    [Fact]
    public async Task ProposeAsync_RequestInspectedByAnotherTechnician_ThrowsNotFoundAndDoesNotSave()
    {
        // Phiếu do KTV khác lập (InspectedByUserId = 5): người dùng 6 không xem được (404) thì cũng không được đề xuất.
        SetupRequest(MakeRequest(DisposalStatusCodes.Inspected));

        await Assert.ThrowsAsync<DisposalRequestNotFoundException>(
            () => _sut.ProposeAsync(1, new ProposeDisposalRequestDto { Reason = "x" }, 6));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(DisposalStatusCodes.Proposed)]
    [InlineData(DisposalStatusCodes.Approved)]
    [InlineData(DisposalStatusCodes.Rejected)]
    [InlineData(DisposalStatusCodes.Completed)]
    public async Task ProposeAsync_WrongStatus_ThrowsConflictAndDoesNotSave(string code)
    {
        SetupRequest(MakeRequest(code));

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.ProposeAsync(1, new ProposeDisposalRequestDto { Reason = "x" }, 5));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ----- Approve / Reject (bước 3: Manager) -----

    [Fact]
    public async Task ApproveAsync_ProposedRequestInOwnDepartment_MovesToApproved()
    {
        var request = MakeRequest(DisposalStatusCodes.Proposed, DeptIt);
        SetupRequest(request);

        var result = await _sut.ApproveAsync(1, new ReviewDisposalRequestDto { Note = "OK" }, currentUserId: 9, currentUserDepartmentId: DeptIt);

        Assert.Equal(DisposalStatusCodes.Approved, result.StatusCode);
        Assert.Equal(9, request.ReviewedByUserId);
    }

    [Fact]
    public async Task ApproveAsync_OtherDepartment_ThrowsNotFound()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptHr));

        await Assert.ThrowsAsync<DisposalRequestNotFoundException>(
            () => _sut.ApproveAsync(1, new ReviewDisposalRequestDto(), 9, DeptIt));
    }

    [Fact]
    public async Task ApproveAsync_NotYetProposed_ThrowsConflict()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Inspected, DeptIt));

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.ApproveAsync(1, new ReviewDisposalRequestDto(), 9, DeptIt));
    }

    [Fact]
    public async Task RejectAsync_WithNote_MovesToRejected()
    {
        var request = MakeRequest(DisposalStatusCodes.Proposed, DeptIt);
        SetupRequest(request);

        var result = await _sut.RejectAsync(1, new ReviewDisposalRequestDto { Note = "Còn sửa được" }, 9, DeptIt);

        Assert.Equal(DisposalStatusCodes.Rejected, result.StatusCode);
        Assert.Equal("Còn sửa được", request.ReviewNote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task RejectAsync_WithoutNote_ThrowsArgumentException(string? note)
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptIt));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.RejectAsync(1, new ReviewDisposalRequestDto { Note = note }, 9, DeptIt));

        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ----- CompleteAsync (bước 4: Admin IT) -----

    [Fact]
    public async Task CompleteAsync_ApprovedRequest_DisposesAssetAndCompletesInOneSave()
    {
        var request = MakeRequest(DisposalStatusCodes.Approved);
        SetupRequest(request);

        var result = await _sut.CompleteAsync(1, new CompleteDisposalRequestDto { Note = "Đã bàn giao" }, currentUserId: 1);

        Assert.Equal(DisposalStatusCodes.Completed, result.StatusCode);
        Assert.Equal(AssetStatus.Disposed, request.Asset.Status);
        Assert.Equal(1, request.CompletedByUserId);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once); // tài sản + phiếu cùng 1 lần lưu (atomic).
    }

    [Fact]
    public async Task CompleteAsync_NotApproved_ThrowsConflictAndKeepsAsset()
    {
        var request = MakeRequest(DisposalStatusCodes.Proposed);
        SetupRequest(request);

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.CompleteAsync(1, new CompleteDisposalRequestDto(), 1));

        Assert.Equal(AssetStatus.Broken, request.Asset.Status);
    }

    [Fact]
    public async Task CompleteAsync_AssetHasOpenAllocation_ThrowsAndKeepsAsset()
    {
        var request = MakeRequest(DisposalStatusCodes.Approved);
        SetupRequest(request);
        _allocationRepoMock.Setup(r => r.HasOpenAllocationAsync(7)).ReturnsAsync(true);

        await Assert.ThrowsAsync<AssetHasOpenAllocationException>(
            () => _sut.CompleteAsync(1, new CompleteDisposalRequestDto(), 1));

        Assert.Equal(AssetStatus.Broken, request.Asset.Status);
        _repoMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // ----- SetSubStatusAsync -----

    [Fact]
    public async Task SetSubStatusAsync_CustomStatus_Assigns()
    {
        var request = MakeRequest(DisposalStatusCodes.Approved);
        SetupRequest(request);
        var custom = new DisposalStatus { Id = 20, Code = "custom-1", Name = "Chờ chứng từ", Color = "warning", IsSystem = false };
        _statusRepoMock.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(custom);

        var result = await _sut.SetSubStatusAsync(1, 20, 1);

        Assert.Equal("Chờ chứng từ", result.SubStatusName);
        Assert.Equal(DisposalStatusCodes.Approved, result.StatusCode); // luồng không đổi.
    }

    [Fact]
    public async Task SetSubStatusAsync_SystemStatus_ThrowsArgumentException()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Approved));
        _statusRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(Statuses[DisposalStatusCodes.Approved]);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.SetSubStatusAsync(1, 3, 1));
    }

    [Theory]
    [InlineData(DisposalStatusCodes.Rejected)]
    [InlineData(DisposalStatusCodes.Completed)]
    public async Task SetSubStatusAsync_FinishedRequest_ThrowsConflict(string code)
    {
        SetupRequest(MakeRequest(code));

        await Assert.ThrowsAsync<DisposalConflictException>(() => _sut.SetSubStatusAsync(1, 20, 1));
    }

    [Fact]
    public async Task SetSubStatusAsync_Null_ClearsSubStatus()
    {
        var request = MakeRequest(DisposalStatusCodes.Approved);
        request.SubStatusId = 20;
        request.SubStatus = new DisposalStatus { Id = 20, Name = "Chờ chứng từ" };
        SetupRequest(request);

        var result = await _sut.SetSubStatusAsync(1, null, 1);

        Assert.Null(result.SubStatusId);
    }

    // ----- phạm vi xem -----

    [Fact]
    public async Task GetByIdAsync_ManagerOtherDepartment_ThrowsNotFound()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptHr));

        await Assert.ThrowsAsync<DisposalRequestNotFoundException>(() => _sut.GetByIdAsync(1, "Manager", DeptIt, 9));
    }

    [Fact]
    public async Task GetByIdAsync_Admin_SeesAnyDepartment()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptHr));

        var result = await _sut.GetByIdAsync(1, "Admin IT", DeptIt, 1);

        Assert.Equal(DeptHr, result.DepartmentId);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianOwnRequest_AnyDepartment_Succeeds()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptHr)); // InspectedByUserId = 5

        var result = await _sut.GetByIdAsync(1, "Technician", DeptIt, currentUserId: 5);

        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_TechnicianOtherTechniciansRequest_ThrowsNotFound()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Proposed, DeptIt)); // do kỹ thuật viên 5 kiểm tra

        await Assert.ThrowsAsync<DisposalRequestNotFoundException>(
            () => _sut.GetByIdAsync(1, "Technician", DeptIt, currentUserId: 6));
    }

    [Fact]
    public async Task GetPagedAsync_Manager_IsForcedToOwnDepartment()
    {
        _repoMock.Setup(r => r.GetPagedAsync(DeptIt, null, null, null, 1, 20)).ReturnsAsync((new List<DisposalRequest>(), 0));

        await _sut.GetPagedAsync(DeptHr, null, null, 1, 20, "Manager", DeptIt, 9);

        _repoMock.Verify(r => r.GetPagedAsync(DeptIt, null, null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Technician_OnlyOwnRequests()
    {
        _repoMock.Setup(r => r.GetPagedAsync(DeptHr, null, null, 6, 1, 20)).ReturnsAsync((new List<DisposalRequest>(), 0));

        await _sut.GetPagedAsync(DeptHr, null, null, 1, 20, "Technician", DeptIt, currentUserId: 6);

        _repoMock.Verify(r => r.GetPagedAsync(DeptHr, null, null, 6, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Admin_NotScopedToTechnician()
    {
        _repoMock.Setup(r => r.GetPagedAsync(null, null, null, null, 1, 20)).ReturnsAsync((new List<DisposalRequest>(), 0));

        await _sut.GetPagedAsync(null, null, null, 1, 20, "Admin IT", null, 1);

        _repoMock.Verify(r => r.GetPagedAsync(null, null, null, null, 1, 20), Times.Once);
    }

    // ----- Thanh lý -> đồng bộ phiếu bảo trì / bước đã bị xoá / hàng đợi tài sản hỏng -----

    [Fact]
    public async Task CompleteAsync_ClosesPendingMaintenanceTicketsOfTheAsset()
    {
        var request = MakeRequest(DisposalStatusCodes.Approved);
        SetupRequest(request);
        var pending = new MaintenanceTicket { Id = 3, AssetId = 7, Status = TicketStatus.Pending };
        _ticketRepoMock.Setup(r => r.GetPendingByAssetAsync(7)).ReturnsAsync(new List<MaintenanceTicket> { pending });

        await _sut.CompleteAsync(1, new CompleteDisposalRequestDto(), 1);

        Assert.Equal(AssetStatus.Disposed, request.Asset.Status);
        Assert.Equal(TicketStatus.Failed, pending.Status);
        Assert.NotNull(pending.ResolvedDate);
    }

    [Fact]
    public async Task ProposeAsync_WhenTargetStepWasDeleted_ThrowsConflictNotServerError()
    {
        SetupRequest(MakeRequest(DisposalStatusCodes.Inspected));
        _statusRepoMock.Setup(r => r.GetByCodeAsync(DisposalStatusCodes.Proposed)).ReturnsAsync((DisposalStatus?)null);

        await Assert.ThrowsAsync<DisposalConflictException>(
            () => _sut.ProposeAsync(1, new ProposeDisposalRequestDto { Reason = "x" }, 5));
    }

    [Fact]
    public async Task GetCandidatesAsync_Manager_IsForcedToOwnDepartment()
    {
        _repoMock.Setup(r => r.GetDisposalCandidatesAsync(DeptIt, 1, 20)).ReturnsAsync((new List<Asset>(), 0));

        await _sut.GetCandidatesAsync(1, 20, "Manager", DeptIt);

        _repoMock.Verify(r => r.GetDisposalCandidatesAsync(DeptIt, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GetCandidatesAsync_Technician_SeesAllDepartments()
    {
        _repoMock.Setup(r => r.GetDisposalCandidatesAsync(null, 1, 20)).ReturnsAsync((new List<Asset>
        {
            new() { Id = 7, AssetCode = "PC-007", Name = "May", DepartmentId = DeptHr, Department = new Department { Id = DeptHr, Name = "HR" },
                    Category = new AssetCategory { Id = 1, Name = "Laptop" } },
        }, 1));

        var result = await _sut.GetCandidatesAsync(1, 20, "Technician", DeptIt);

        Assert.Single(result.Items);
        Assert.Equal("PC-007", result.Items[0].AssetCode);
        Assert.Equal("HR", result.Items[0].DepartmentName);
    }
}
