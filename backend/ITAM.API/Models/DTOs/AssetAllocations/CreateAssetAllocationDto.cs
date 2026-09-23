namespace ITAM.API.Models.DTOs.AssetAllocations;

public class CreateAssetAllocationDto
{
    public int AssetId { get; set; }
    public int DepartmentId { get; set; }
    public string RecipientName { get; set; } = null!;
    public DateOnly AllocatedDate { get; set; }
    public string? HandoverNote { get; set; }
}
