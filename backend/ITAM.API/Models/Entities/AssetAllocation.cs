using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

public class AssetAllocation
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int DepartmentId { get; set; }
    public string RecipientName { get; set; } = null!;
    public DateOnly AllocatedDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public string? HandoverNote { get; set; }
    public AllocationStatus Status { get; set; }

    public Asset Asset { get; set; } = null!;
    public Department Department { get; set; } = null!;
}
