namespace ITAM.API.Models.DTOs.AssetAllocations;

public class OverdueAllocationDto
{
    public int AllocationId { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public DateOnly? LastMaintenanceDate { get; set; }
    public int DaysSinceLastMaintenance { get; set; }
}
