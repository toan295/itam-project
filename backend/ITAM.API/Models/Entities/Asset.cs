using ITAM.API.Models.Enums;

namespace ITAM.API.Models.Entities;

public class Asset
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int CategoryId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specification { get; set; }
    public string? OperatingSystem { get; set; }
    public int DepartmentId { get; set; }
    public AssetStatus Status { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AssetCategory Category { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<AssetSoftwareLicense> AssetSoftwareLicenses { get; set; } = new List<AssetSoftwareLicense>();
    public ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
    public ICollection<AssetAllocation> AssetAllocations { get; set; } = new List<AssetAllocation>();
}
