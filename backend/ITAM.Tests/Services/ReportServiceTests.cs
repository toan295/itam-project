using ITAM.API.Authorization;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Common;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.Enums;
using ITAM.API.Reporting;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Moq;

namespace ITAM.Tests.Services;

public class ReportServiceTests
{
    private static readonly int Year = VietnamTime.Today().Year;

    private readonly Mock<IReportRepository> _repo = new();
    private readonly Mock<ILifecycleAnalysisService> _lifecycle = new();
    private readonly ReportService _sut;

    public ReportServiceTests()
    {
        _sut = new ReportService(_repo.Object, _lifecycle.Object);
        _repo.Setup(r => r.GetUserNameAsync(7)).ReturnsAsync("Nguyễn Văn Admin");
        _repo.Setup(r => r.GetDepartmentNameAsync(It.IsAny<int>())).ReturnsAsync((int id) => id is 1 or 2 ? $"Phòng {id}" : null);
        _repo.Setup(r => r.GetCategoryNameAsync(It.IsAny<int>())).ReturnsAsync((int id) => id == 10 ? "Máy tính" : null);
        _repo.Setup(r => r.GetInventoryAsync(It.IsAny<InventoryFilter>(), It.IsAny<int>())).ReturnsAsync(new List<InventoryRow>
        {
            new("PC-001", "Máy tính A", "Máy tính", "Phòng 1", "SN1", AssetStatus.InUse, new DateOnly(2024, 1, 5), new DateOnly(2027, 1, 5), "Lê Văn Giữ"),
            new("PC-002", "=cmd()", "Máy tính", "Phòng 1", null, AssetStatus.Broken, null, null, null),
        });
        _repo.Setup(r => r.GetOpenTicketsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>())).ReturnsAsync(new List<OpenTicketRow>());
        _repo.Setup(r => r.GetWarrantyExpiringAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>())).ReturnsAsync(new List<WarrantyRow>());
        _lifecycle.Setup(l => l.GetAllCurrentReplacementCandidatesAsync(It.IsAny<int?>(), It.IsAny<int?>())).ReturnsAsync(new List<ReplacementRecommendationDto>());
        _lifecycle.Setup(l => l.GetAllUpcomingAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>())).ReturnsAsync(new List<UpcomingReplacementDto>());
        _repo.Setup(r => r.GetLicensesAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseRow>());
        _repo.Setup(r => r.GetLicenseAssignmentsAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseAssignmentRow>());
    }

    private static ReportRequest Admin(int? dept = null, int? category = null, string? status = null, int? purchaseYear = null, int? months = null, int? days = null, string? keyword = null) =>
        new(7, PermissionCatalog.AdminRole, 1, dept, category, status, purchaseYear, keyword, months, days);

    private static ReportRequest Manager(int? own, int? requested = null) => new(8, PermissionCatalog.ManagerRole, own, requested);

    private static string Meta(ReportDocument doc, string label) => doc.Meta.Single(m => m.Label == label).Value;

    // ---------- Phạm vi ----------

    [Fact]
    public async Task Admin_WithoutFilter_ExportsWholeSystemAndSaysSo()
    {
        var doc = await _sut.BuildInventoryAsync(Admin());

        Assert.Equal("Toàn hệ thống", Meta(doc, "Phạm vi"));
        Assert.Equal("Nguyễn Văn Admin", Meta(doc, "Người xuất"));
        Assert.Equal("Không (toàn bộ)", Meta(doc, "Bộ lọc"));
        _repo.Verify(r => r.GetInventoryAsync(It.Is<InventoryFilter>(f => f.DepartmentId == null), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Manager_IsLockedToOwnDepartment_InEveryReport()
    {
        await _sut.BuildInventoryAsync(Manager(1));
        await _sut.BuildMaintenanceDueAsync(Manager(1));
        await _sut.BuildShortLifecycleAsync(Manager(1));

        _repo.Verify(r => r.GetInventoryAsync(It.Is<InventoryFilter>(f => f.DepartmentId == 1), It.IsAny<int>()), Times.Once);
        _repo.Verify(r => r.GetOpenTicketsAsync(1, null, It.IsAny<int>()), Times.Once);
        _repo.Verify(r => r.GetWarrantyExpiringAsync(1, null, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>()), Times.Once);
        _lifecycle.Verify(l => l.GetAllCurrentReplacementCandidatesAsync(1, null), Times.Once);
        _lifecycle.Verify(l => l.GetAllUpcomingAsync(1, null, 12), Times.Once);
    }

    [Fact]
    public async Task Manager_RequestingAnotherDepartment_IsForbiddenInEveryReportAndNothingIsQueried()
    {
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.BuildInventoryAsync(Manager(1, 2)));
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.BuildMaintenanceDueAsync(Manager(1, 2)));
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.BuildShortLifecycleAsync(Manager(1, 2)));

        _repo.Verify(r => r.GetInventoryAsync(It.IsAny<InventoryFilter>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task NonAdminWithoutDepartmentClaim_IsForbiddenNotWholeSystem()
    {
        await Assert.ThrowsAsync<DepartmentForbiddenException>(() => _sut.BuildInventoryAsync(Manager(null)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task Admin_UnknownDepartment_IsRejected(int dept)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(dept: dept)));
    }

    // ---------- Validate ----------

    [Theory]
    [InlineData("Hong")]
    [InlineData("3x")]
    [InlineData("99")]
    public async Task Inventory_InvalidStatus_IsRejected(string status)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(status: status)));
    }

    [Theory]
    [InlineData(1989)]
    [InlineData(0)]
    public async Task Inventory_PurchaseYearOutOfRange_IsRejected(int year)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(purchaseYear: year)));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(purchaseYear: Year + 1)));
    }

    [Fact]
    public async Task UnknownCategory_IsRejectedInEveryReport()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(category: 99)));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildMaintenanceDueAsync(Admin(category: 99)));
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildShortLifecycleAsync(Admin(category: 99)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task MaintenanceDue_DaysOutOfRange_IsRejected(int days)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildMaintenanceDueAsync(Admin(days: days)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task ShortLifecycle_MonthsOutOfRange_IsRejected(int months)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildShortLifecycleAsync(Admin(months: months)));
    }

    // ---------- Tồn kho ----------

    [Fact]
    public async Task Inventory_ProducesTypedRowsWithVietnameseStatusesAndTotals()
    {
        var doc = await _sut.BuildInventoryAsync(Admin(category: 10, status: "inuse"));

        var table = Assert.Single(doc.Tables);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Đang sử dụng", table.Rows[0][5]);
        Assert.Equal(new DateOnly(2024, 1, 5), table.Rows[0][6]);                 // ngày là ngày thật
        Assert.Equal("Lê Văn Giữ", table.Rows[0][8]);
        Assert.Contains("Tổng cộng: 2 thiết bị", table.Note);
        Assert.Contains("Loại tài sản: Máy tính", Meta(doc, "Bộ lọc"));
        Assert.Contains("Trạng thái: Đang sử dụng", Meta(doc, "Bộ lọc"));
        Assert.Contains(doc.Warnings, w => w.Contains("1 thiết bị chưa có ngày mua"));
        _repo.Verify(r => r.GetInventoryAsync(It.Is<InventoryFilter>(f => f.Status == AssetStatus.InUse && f.CategoryId == 10), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Inventory_OverTheRowLimit_ThrowsInsteadOfBuildingHugeFile()
    {
        _repo.Setup(r => r.GetInventoryAsync(It.IsAny<InventoryFilter>(), It.IsAny<int>()))
            .ReturnsAsync(Enumerable.Range(0, ReportExporter.MaxRows + 1).Select(i => new InventoryRow($"A{i}", "n", "c", "d", null, AssetStatus.InUse, null, null, null)).ToList());

        await Assert.ThrowsAsync<ReportTooLargeException>(() => _sut.BuildInventoryAsync(Admin()));
    }

    [Fact]
    public async Task Inventory_LongKeyword_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildInventoryAsync(Admin(keyword: new string('x', 101))));
    }

    // ---------- Sắp bảo trì ----------

    [Fact]
    public async Task MaintenanceDue_ComputesDaysOpenAndDaysLeftOnVietnamCalendar()
    {
        var today = VietnamTime.Today();
        _repo.Setup(r => r.GetOpenTicketsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>())).ReturnsAsync(new List<OpenTicketRow>
        {
            new(5, "PC-001", "Máy A", "Máy tính", "Phòng 1", TicketPriority.Urgent, DateTime.UtcNow.AddDays(-3), null, new string('x', 400)),
        });
        _repo.Setup(r => r.GetWarrantyExpiringAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int>())).ReturnsAsync(new List<WarrantyRow>
        {
            new("PC-002", "Máy B", "Máy tính", "Phòng 1", AssetStatus.InUse, null, today.AddDays(10)),
        });

        var doc = await _sut.BuildMaintenanceDueAsync(Admin(days: 30));

        Assert.Equal(new[] { "Phiếu đang xử lý", "Bảo hành sắp hết" }, doc.Tables.Select(t => t.Name));
        var ticket = doc.Tables[0].Rows[0];
        Assert.Equal(("Khẩn cấp", 3, "Chưa phân công"), ((string)ticket[5]!, (int)ticket[7]!, (string)ticket[8]!));
        Assert.EndsWith("…", (string)ticket[9]!);                               // mô tả dài bị cắt
        Assert.Equal(10, doc.Tables[1].Rows[0][7]);
        _repo.Verify(r => r.GetWarrantyExpiringAsync(null, null, today, today.AddDays(30), It.IsAny<int>()), Times.Once);
        Assert.Contains("30 ngày", Meta(doc, "Bộ lọc"));
    }

    // ---------- Vòng đời ngắn ----------

    [Fact]
    public async Task ShortLifecycle_UsesLifecycleModuleListsUnchanged()
    {
        _lifecycle.Setup(l => l.GetAllCurrentReplacementCandidatesAsync(2, null)).ReturnsAsync(new List<ReplacementRecommendationDto>
        {
            new() { AssetCode = "PC-9", AssetName = "Máy", CategoryName = "Máy tính", DepartmentName = "Phòng 2", PriorityScore = 80, ReasonText = "Quá tuổi", AgeYears = 6.54, IsUnderWarranty = true, TicketCount = 4 },
        });
        _lifecycle.Setup(l => l.GetAllUpcomingAsync(2, null, 6)).ReturnsAsync(new List<UpcomingReplacementDto>
        {
            new() { AssetCode = "PC-8", AssetName = "Máy 8", CategoryName = "Máy tính", DepartmentName = "Phòng 2", DaysUntilAgeDue = 30, ReasonText = "Sắp đủ tuổi" },
        });

        var doc = await _sut.BuildShortLifecycleAsync(Admin(dept: 2, months: 6));

        Assert.Equal(new[] { "Đã vượt ngưỡng", "Sắp tới hạn" }, doc.Tables.Select(t => t.Name));
        var row = doc.Tables[0].Rows[0];
        Assert.Equal((80, "Còn bảo hành", 6.5m), ((int)row[7]!, (string)row[9]!, (decimal)row[5]!));
        Assert.Equal(30, doc.Tables[1].Rows[0][7]);
        Assert.Equal("Phòng 2", Meta(doc, "Phạm vi"));
    }

    // ---------- Ngân sách ----------

    // ---------- Phần mềm & Giấy phép ----------

    private static LicenseRow Lic(int id, string name, int daysFromToday, int max = 5, int used = 1) =>
        new(id, name, $"KEY-{id}", VietnamTime.Today().AddDays(daysFromToday), max, used, null);

    [Fact]
    public async Task Licenses_ClassifiesByExpiryAndListsAssignments()
    {
        _repo.Setup(r => r.GetLicensesAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseRow>
        { Lic(1, "Word", -5), Lic(2, "Visio", 10), Lic(3, "Office", 200) });
        _repo.Setup(r => r.GetLicenseAssignmentsAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseAssignmentRow>
        { new(1, "Word", "PC-1", "Máy A", "Phòng 1", VietnamTime.Today()), new(3, "Office", "PC-2", "Máy B", "Phòng 2", VietnamTime.Today()) });

        var doc = await _sut.BuildLicensesAsync(Admin());

        Assert.Equal(new[] { "Đã hết hạn", "Sắp hết hạn", "Còn hạn" }, doc.Tables[0].Rows.Select(r => (string)r[3]!).ToArray());
        Assert.Equal(2, doc.Tables[1].Rows.Count);
        Assert.Equal("Toàn hệ thống", Meta(doc, "Phạm vi"));
    }

    [Theory]
    [InlineData("expired", 1)]
    [InlineData("expiring", 1)]
    [InlineData("valid", 1)]
    public async Task Licenses_StateFilter_KeepsOnlyMatchingLicensesAndTheirAssignments(string state, int expected)
    {
        _repo.Setup(r => r.GetLicensesAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseRow> { Lic(1, "Word", -5), Lic(2, "Visio", 10), Lic(3, "Office", 200) });
        _repo.Setup(r => r.GetLicenseAssignmentsAsync(It.IsAny<int>())).ReturnsAsync(new List<LicenseAssignmentRow>
        { new(1, "Word", "PC-1", "A", "P", VietnamTime.Today()), new(2, "Visio", "PC-2", "B", "P", VietnamTime.Today()), new(3, "Office", "PC-3", "C", "P", VietnamTime.Today()) });

        var doc = await _sut.BuildLicensesAsync(Admin(status: state));

        Assert.Equal(expected, doc.Tables[0].Rows.Count);
        Assert.Equal(expected, doc.Tables[1].Rows.Count);
    }

    [Fact]
    public async Task Licenses_InvalidState_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.BuildLicensesAsync(Admin(status: "abc")));
    }

    // ---------- Xuất file thật từ mọi báo cáo ----------

    [Fact]
    public async Task EveryReport_RendersToValidExcelAndPdf()
    {
        var exporter = new ReportExporter();
        foreach (var doc in new[]
        {
            await _sut.BuildInventoryAsync(Admin()), await _sut.BuildMaintenanceDueAsync(Admin()),
            await _sut.BuildShortLifecycleAsync(Admin()), await _sut.BuildLicensesAsync(Admin()),
        })
        {
            Assert.StartsWith("PK", System.Text.Encoding.ASCII.GetString(exporter.Export(doc, ReportFormat.Excel, "x").Content, 0, 2));
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(exporter.Export(doc, ReportFormat.Pdf, "x").Content, 0, 4));
        }
    }
}
