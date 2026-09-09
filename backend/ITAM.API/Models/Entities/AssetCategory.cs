namespace ITAM.API.Models.Entities;

public class AssetCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
