namespace ITAM.API.Models.DTOs.Lifecycle;

public class AssetLifecycleRow
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public DateOnly? PurchaseDate { get; set; }
    public int TicketCount { get; set; }
    public int FailedTicketCount { get; set; }
}
