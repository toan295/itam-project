namespace ITAM.API.Models.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<AssetAllocation> AssetAllocations { get; set; } = new List<AssetAllocation>();
    public ICollection<BudgetForecast> BudgetForecasts { get; set; } = new List<BudgetForecast>();
}
