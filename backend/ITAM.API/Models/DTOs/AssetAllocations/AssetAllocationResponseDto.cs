namespace ITAM.API.Models.DTOs.AssetAllocations;

public class AssetAllocationResponseDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public int? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeePosition { get; set; }
    public DateOnly AllocatedDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public string? HandoverNote { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverLocation { get; set; }
    public string HandoverCondition { get; set; } = "";
    public string? ReturnCondition { get; set; }
    public string? ReturnNote { get; set; }
    public string Status { get; set; } = "";
}
