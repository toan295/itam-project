namespace ITAM.API.Models.DTOs.Dashboard;

public class DashboardExpiringLicenseDto
{
    public string SoftwareName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsNearUsageLimit { get; set; }
}

public class DashboardSummaryDto
{
    public int? ActiveAllocations { get; set; }
    public int? SoftwareLicenseCount { get; set; }
    public int? ExpiringSoftwareLicenses { get; set; }
    public IReadOnlyList<DashboardExpiringLicenseDto>? ExpiringSoftwareLicenseItems { get; set; }
    public int? ReplacementCandidates { get; set; }
}
