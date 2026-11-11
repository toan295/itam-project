namespace ITAM.API.Models.DTOs.SoftwareLicenses;

// Một lượt gán license cho tài sản (một "chỗ" trong MaxUsage).
public class LicenseAssignmentDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;
    public DateOnly AssignedDate { get; set; }
}
