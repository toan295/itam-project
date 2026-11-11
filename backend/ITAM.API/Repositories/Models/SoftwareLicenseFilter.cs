namespace ITAM.API.Repositories.Models;

/// <summary>Trạng thái license dùng để lọc danh sách (các nhóm loại trừ nhau, trừ NearLimit).</summary>
public enum LicenseStateFilter
{
    /// <summary>Chưa hết hạn và chưa vào cửa sổ cảnh báo.</summary>
    Valid,
    /// <summary>Còn hạn nhưng sẽ hết hạn trong cửa sổ cảnh báo.</summary>
    ExpiringSoon,
    /// <summary>Đã quá ngày hết hạn.</summary>
    Expired,
    /// <summary>Số lượt dùng từ 80% giới hạn nhưng chưa hết chỗ (hết chỗ dùng bộ lọc Assignment=Full).</summary>
    NearLimit,
}

public enum LicenseSortBy
{
    Name,
    Expiry,
    Usage,
    Newest,
}

/// <summary>Tình trạng gán license cho tài sản: chưa gán, đã gán (còn chỗ hoặc hết), hết chỗ.</summary>
public enum LicenseAssignmentFilter
{
    /// <summary>Chưa gán cho tài sản nào.</summary>
    Unused,
    /// <summary>Đã gán cho ít nhất một tài sản.</summary>
    InUse,
    /// <summary>Đã dùng hết số lượt (số tài sản được gán &gt;= MaxUsage).</summary>
    Full,
}

/// <summary>Bộ lọc nâng cao cho danh sách license. Service đã parse/validate — Repository chỉ áp dụng.</summary>
public class SoftwareLicenseFilter
{
    public string? Search { get; set; }
    public LicenseStateFilter? State { get; set; }
    public DateOnly? ExpiryFrom { get; set; }
    public DateOnly? ExpiryTo { get; set; }
    public LicenseSortBy SortBy { get; set; } = LicenseSortBy.Name;
    public LicenseAssignmentFilter? Assignment { get; set; }

    /// <summary>Hôm nay và hết cửa sổ cảnh báo — Service cung cấp để Repository không tự đọc đồng hồ.</summary>
    public DateOnly Today { get; set; }
    public DateOnly WarningDate { get; set; }
}
