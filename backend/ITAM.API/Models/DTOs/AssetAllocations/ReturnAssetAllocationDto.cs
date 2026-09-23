namespace ITAM.API.Models.DTOs.AssetAllocations;

public class ReturnAssetAllocationDto
{
    public DateOnly ReturnedDate { get; set; }
    public string Condition { get; set; } = null!;
    public string? ReturnNote { get; set; }
}
