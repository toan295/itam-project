namespace ITAM.API.Models.Entities;

public class AssetCategoryLifecyclePolicy
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }

    public AssetCategory Category { get; set; } = null!;
}
