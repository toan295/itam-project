namespace ITAM.API.Models.DTOs.Lifecycle;

public class TopFailingAssetDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string DepartmentName { get; set; } = null!;
    public int TicketCount { get; set; }
    public int FailedTicketCount { get; set; }
}
