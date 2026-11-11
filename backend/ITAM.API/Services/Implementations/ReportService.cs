using System.Globalization;
using ITAM.API.Helpers;
using ITAM.API.Models.Enums;
using ITAM.API.Reporting;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class ReportService : IReportService
{
    public const int DefaultWarrantyDays = 90;
    public const int DefaultUpcomingMonths = 12;
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly Dictionary<AssetStatus, string> StatusLabels = new()
    {
        [AssetStatus.InUse] = "Đang sử dụng",
        [AssetStatus.Maintenance] = "Đang bảo trì",
        [AssetStatus.Broken] = "Hỏng",
        [AssetStatus.Disposed] = "Đã thanh lý",
    };

    private static readonly Dictionary<TicketPriority, string> PriorityLabels = new()
    {
        [TicketPriority.Low] = "Thấp",
        [TicketPriority.Normal] = "Bình thường",
        [TicketPriority.High] = "Cao",
        [TicketPriority.Urgent] = "Khẩn cấp",
    };

    private readonly IReportRepository _repo;
    private readonly ILifecycleAnalysisService _lifecycle;

    public ReportService(IReportRepository repo, ILifecycleAnalysisService lifecycle)
    {
        _repo = repo;
        _lifecycle = lifecycle;
    }

    // ---------------------------------------------------------------- Tồn kho thiết bị

    public async Task<ReportDocument> BuildInventoryAsync(ReportRequest request)
    {
        var scope = await ResolveScopeAsync(request);
        var categoryName = await ValidateCategoryAsync(request.CategoryId);
        var status = ParseStatus(request.Status);
        if (request.PurchaseYear is { } py && (py < DateRules.MinPurchaseYear || py > VietnamTime.Today().Year))
            throw new ArgumentException($"Năm mua phải từ {DateRules.MinPurchaseYear} đến {VietnamTime.Today().Year}.");
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        if (keyword is { Length: > 100 }) throw new ArgumentException("Từ khoá tối đa 100 ký tự.");

        var rows = await _repo.GetInventoryAsync(new InventoryFilter(scope.DepartmentId, request.CategoryId, status, request.PurchaseYear, keyword), ReportExporter.MaxRows);
        EnsureWithinLimit(rows.Count);

        var doc = await NewDocumentAsync("Báo cáo tồn kho thiết bị", request, scope);
        AddFilterMeta(doc, ("Loại tài sản", categoryName), ("Trạng thái", status is null ? null : StatusLabels[status.Value]),
            ("Năm mua", request.PurchaseYear?.ToString()), ("Từ khoá", keyword));

        var withoutDate = rows.Count(r => r.PurchaseDate is null);
        if (withoutDate > 0) doc.Warnings.Add($"{withoutDate} thiết bị chưa có ngày mua.");

        doc.Tables.Add(new ReportTable("Tồn kho",
            new[]
            {
                new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 2.2), new ReportColumn("Loại", Width: 1.3),
                new ReportColumn("Phòng ban", Width: 1.3), new ReportColumn("Số serial", Width: 1.2), new ReportColumn("Trạng thái", Width: 1),
                new ReportColumn("Ngày mua", ReportColumnKind.Date, 1), new ReportColumn("Hết bảo hành", ReportColumnKind.Date, 1), new ReportColumn("Người đang giữ", Width: 1.4),
            },
            rows.Select(r => new object?[]
            {
                r.AssetCode, r.Name, r.CategoryName, r.DepartmentName, r.SerialNumber, StatusLabels[r.Status], r.PurchaseDate, r.WarrantyExpiry, r.Holder,
            }).ToList())
        { Note = $"Tổng cộng: {rows.Count:N0} thiết bị" + string.Join("", Enum.GetValues<AssetStatus>().Select(s => rows.Count(r => r.Status == s) is var n and > 0 ? $" · {StatusLabels[s]}: {n:N0}" : "")) });
        return doc;
    }

    // ---------------------------------------------------------------- Sắp bảo trì

    public async Task<ReportDocument> BuildMaintenanceDueAsync(ReportRequest request)
    {
        var scope = await ResolveScopeAsync(request);
        var categoryName = await ValidateCategoryAsync(request.CategoryId);
        var days = request.Days ?? DefaultWarrantyDays;
        if (days is < 1 or > 365) throw new ArgumentException("Số ngày bảo hành sắp hết phải từ 1 đến 365.");

        var today = VietnamTime.Today();
        var tickets = await _repo.GetOpenTicketsAsync(scope.DepartmentId, request.CategoryId, ReportExporter.MaxRows);
        var warranties = await _repo.GetWarrantyExpiringAsync(scope.DepartmentId, request.CategoryId, today, today.AddDays(days), ReportExporter.MaxRows);
        EnsureWithinLimit(tickets.Count + warranties.Count);

        var doc = await NewDocumentAsync("Báo cáo thiết bị sắp bảo trì", request, scope);
        AddFilterMeta(doc, ("Loại tài sản", categoryName), ("Bảo hành hết trong", $"{days} ngày tới"));
        doc.Meta.Add(("Định nghĩa", "Phiếu bảo trì đang xử lý (tài sản chưa thanh lý) và thiết bị có bảo hành còn hiệu lực nhưng sắp hết. Hệ thống chưa có lịch bảo trì định kỳ."));

        doc.Tables.Add(new ReportTable("Phiếu đang xử lý",
            new[]
            {
                new ReportColumn("Mã phiếu", ReportColumnKind.Integer), new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 1.8),
                new ReportColumn("Loại", Width: 1.2), new ReportColumn("Phòng ban", Width: 1.2), new ReportColumn("Mức độ", Width: 1),
                new ReportColumn("Ngày báo", ReportColumnKind.DateTime, 1.2), new ReportColumn("Đã mở (ngày)", ReportColumnKind.Integer), new ReportColumn("Kỹ thuật viên", Width: 1.2),
                new ReportColumn("Mô tả lỗi", Width: 2.5),
            },
            tickets.Select(t => new object?[]
            {
                t.TicketId, t.AssetCode, t.AssetName, t.CategoryName, t.DepartmentName, PriorityLabels[t.Priority], t.ReportedDate,
                Math.Max(0, today.DayNumber - DateOnly.FromDateTime(t.ReportedDate.AddHours(7)).DayNumber), t.TechnicianName ?? "Chưa phân công", Truncate(t.Description, 300),
            }).ToList())
        { Note = $"Tổng cộng: {tickets.Count:N0} phiếu đang xử lý" });

        doc.Tables.Add(new ReportTable("Bảo hành sắp hết",
            new[]
            {
                new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 2), new ReportColumn("Loại", Width: 1.2), new ReportColumn("Phòng ban", Width: 1.2),
                new ReportColumn("Trạng thái", Width: 1), new ReportColumn("Ngày mua", ReportColumnKind.Date), new ReportColumn("Hết bảo hành", ReportColumnKind.Date),
                new ReportColumn("Còn (ngày)", ReportColumnKind.Integer),
            },
            warranties.Select(w => new object?[]
            {
                w.AssetCode, w.Name, w.CategoryName, w.DepartmentName, StatusLabels[w.Status], w.PurchaseDate, w.WarrantyExpiry, w.WarrantyExpiry.DayNumber - today.DayNumber,
            }).ToList())
        { Note = $"Tổng cộng: {warranties.Count:N0} thiết bị có bảo hành hết trong {days} ngày tới" });
        return doc;
    }

    // ---------------------------------------------------------------- Vòng đời ngắn

    public async Task<ReportDocument> BuildShortLifecycleAsync(ReportRequest request)
    {
        var scope = await ResolveScopeAsync(request);
        var categoryName = await ValidateCategoryAsync(request.CategoryId);
        var months = request.Months ?? DefaultUpcomingMonths;
        if (months is < 1 or > 60) throw new ArgumentException("Số tháng cảnh báo phải từ 1 đến 60.");

        // Cùng hàm với trang Vòng đời và Dashboard nên số liệu khớp tuyệt đối.
        var overdue = await _lifecycle.GetAllCurrentReplacementCandidatesAsync(scope.DepartmentId, request.CategoryId);
        var upcoming = await _lifecycle.GetAllUpcomingAsync(scope.DepartmentId, request.CategoryId, months);
        EnsureWithinLimit(overdue.Count + upcoming.Count);

        var doc = await NewDocumentAsync("Báo cáo thiết bị vòng đời ngắn (cần / sắp thay thế)", request, scope);
        AddFilterMeta(doc, ("Loại tài sản", categoryName), ("Sắp tới hạn trong", $"{months} tháng tới"));
        doc.Meta.Add(("Quy tắc", "Cần thay thế khi vượt tuổi tối đa HOẶC vượt số phiếu bảo trì tối đa của loại (tuổi tính từ ngày mua). Điểm ưu tiên chỉ để xếp hạng."));

        doc.Tables.Add(new ReportTable("Đã vượt ngưỡng",
            new[]
            {
                new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 1.8), new ReportColumn("Loại", Width: 1.2), new ReportColumn("Phòng ban", Width: 1.2),
                new ReportColumn("Ngày mua", ReportColumnKind.Date), new ReportColumn("Tuổi (năm)", ReportColumnKind.Decimal), new ReportColumn("Số phiếu", ReportColumnKind.Integer),
                new ReportColumn("Điểm ưu tiên", ReportColumnKind.Integer), new ReportColumn("Lý do", Width: 2.6), new ReportColumn("Bảo hành", Width: 1),
            },
            overdue.Select(r => new object?[]
            {
                r.AssetCode, r.AssetName, r.CategoryName, r.DepartmentName, r.PurchaseDate, r.AgeYears is { } a ? Math.Round((decimal)a, 1) : null,
                r.TicketCount, r.PriorityScore, r.ReasonText, r.IsUnderWarranty ? "Còn bảo hành" : "Hết/không có",
            }).ToList())
        { Note = $"Tổng cộng: {overdue.Count:N0} thiết bị đã vượt ngưỡng (xếp theo điểm ưu tiên giảm dần)" });

        doc.Tables.Add(new ReportTable("Sắp tới hạn",
            new[]
            {
                new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 1.8), new ReportColumn("Loại", Width: 1.2), new ReportColumn("Phòng ban", Width: 1.2),
                new ReportColumn("Tuổi (năm)", ReportColumnKind.Decimal), new ReportColumn("Số phiếu", ReportColumnKind.Integer), new ReportColumn("Ngày đủ tuổi", ReportColumnKind.Date),
                new ReportColumn("Còn (ngày)", ReportColumnKind.Integer), new ReportColumn("Lý do", Width: 2.6),
            },
            upcoming.Select(u => new object?[]
            {
                u.AssetCode, u.AssetName, u.CategoryName, u.DepartmentName, u.AgeYears is { } a ? Math.Round((decimal)a, 1) : null, u.TicketCount, u.AgeDueDate, u.DaysUntilAgeDue, u.ReasonText,
            }).ToList())
        { Note = $"Tổng cộng: {upcoming.Count:N0} thiết bị sắp tới hạn trong {months} tháng" });
        return doc;
    }

    // ---------------------------------------------------------------- Phần mềm & Giấy phép

    public const int LicenseWarningDays = 30;   // khớp ngưỡng "sắp hết hạn" ở màn Phần mềm & Giấy phép

    public async Task<ReportDocument> BuildLicensesAsync(ReportRequest request)
    {
        var state = ParseLicenseState(request.Status);
        var today = VietnamTime.Today();
        var warningDate = today.AddDays(LicenseWarningDays);

        var licenses = await _repo.GetLicensesAsync(ReportExporter.MaxRows);
        EnsureWithinLimit(licenses.Count);
        string StateOf(LicenseRow l) => l.ExpiryDate < today ? "expired" : l.ExpiryDate <= warningDate ? "expiring" : "valid";
        var filtered = state is null ? licenses : licenses.Where(l => StateOf(l) == state).ToList();
        var assignments = await _repo.GetLicenseAssignmentsAsync(ReportExporter.MaxRows);
        EnsureWithinLimit(assignments.Count);
        var ids = filtered.Select(l => l.Id).ToHashSet();
        assignments = assignments.Where(a => ids.Contains(a.LicenseId)).ToList();

        var doc = await NewDocumentAsync("Báo cáo phần mềm & giấy phép", request, (null, "Toàn hệ thống"));
        AddFilterMeta(doc, ("Tình trạng", state switch { "expired" => "Đã hết hạn", "expiring" => $"Sắp hết hạn (trong {LicenseWarningDays} ngày)", "valid" => "Còn hạn", _ => null }));
        doc.Meta.Add(("Định nghĩa", $"Sắp hết hạn = hết hạn trong {LicenseWarningDays} ngày tới. Đã dùng = số tài sản đang được gán license."));

        doc.Tables.Add(new ReportTable("Danh sách license",
            new[]
            {
                new ReportColumn("Tên phần mềm", Width: 2), new ReportColumn("Khoá license", Width: 2), new ReportColumn("Ngày hết hạn", ReportColumnKind.Date),
                new ReportColumn("Tình trạng", Width: 1.2), new ReportColumn("Số máy tối đa", ReportColumnKind.Integer), new ReportColumn("Đã dùng", ReportColumnKind.Integer),
                new ReportColumn("Ghi chú", Width: 2.5),
            },
            filtered.Select(l => new object?[]
            {
                l.SoftwareName, l.LicenseKey, l.ExpiryDate,
                StateOf(l) switch { "expired" => "Đã hết hạn", "expiring" => "Sắp hết hạn", _ => "Còn hạn" },
                l.MaxUsage, l.CurrentUsage, l.Notes is null ? null : Truncate(l.Notes, 300),
            }).ToList())
        { Note = $"Tổng cộng: {filtered.Count:N0} license" });

        doc.Tables.Add(new ReportTable("Tài sản đang dùng license",
            new[]
            {
                new ReportColumn("Tên phần mềm", Width: 2), new ReportColumn("Mã tài sản", Width: 1), new ReportColumn("Tên thiết bị", Width: 2),
                new ReportColumn("Phòng ban", Width: 1.2), new ReportColumn("Ngày gán", ReportColumnKind.Date),
            },
            assignments.Select(a => new object?[] { a.SoftwareName, a.AssetCode, a.AssetName, a.DepartmentName, a.AssignedDate }).ToList())
        { Note = $"Tổng cộng: {assignments.Count:N0} lượt gán" });
        return doc;
    }

    private static string? ParseLicenseState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return v is "expired" or "expiring" or "valid" ? v : throw new ArgumentException("Tình trạng không hợp lệ. Phải là một trong: expired, expiring, valid");
    }

    // ---------------------------------------------------------------- nội bộ

    private async Task<(int? DepartmentId, string DepartmentName)> ResolveScopeAsync(ReportRequest request)
    {
        var (departmentId, _) = ViewerScope.Resolve(request.Role, request.UserDepartmentId, request.DepartmentId);
        if (!departmentId.HasValue)
        {
            return (null, "Toàn hệ thống");
        }

        var name = departmentId.Value > 0 ? await _repo.GetDepartmentNameAsync(departmentId.Value) : null;
        return name is null ? throw new ArgumentException("Phòng ban không tồn tại.") : (departmentId, name);
    }

    private async Task<string?> ValidateCategoryAsync(int? categoryId)
    {
        if (!categoryId.HasValue) return null;
        var name = categoryId.Value > 0 ? await _repo.GetCategoryNameAsync(categoryId.Value) : null;
        return name ?? throw new ArgumentException("Loại tài sản không tồn tại.");
    }

    private static AssetStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        return Enum.TryParse<AssetStatus>(status.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException($"Trạng thái không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<AssetStatus>())}");
    }

    private static void EnsureWithinLimit(int rows)
    {
        if (rows > ReportExporter.MaxRows) throw new ReportTooLargeException(rows, ReportExporter.MaxRows);
    }

    private async Task<ReportDocument> NewDocumentAsync(string title, ReportRequest request, (int? DepartmentId, string DepartmentName) scope)
    {
        var doc = new ReportDocument { Title = title };
        doc.Meta.Add(("Người xuất", await _repo.GetUserNameAsync(request.UserId) ?? $"Người dùng #{request.UserId}"));
        doc.Meta.Add(("Thời điểm xuất", DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm", Vi) + " (giờ Việt Nam)"));
        doc.Meta.Add(("Phạm vi", scope.DepartmentName));
        return doc;
    }

    private static void AddFilterMeta(ReportDocument doc, params (string Label, string? Value)[] filters)
    {
        var used = filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Select(f => $"{f.Label}: {f.Value}").ToList();
        doc.Meta.Add(("Bộ lọc", used.Count == 0 ? "Không (toàn bộ)" : string.Join("; ", used)));
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
