namespace ITAM.API.Models.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    // Xoá mềm: bấm "Xoá" trên giao diện chỉ đánh dấu dòng này, dữ liệu vẫn còn trong CSDL (và không hiện lại khi chạy lại hệ thống).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<AssetAllocation> AssetAllocations { get; set; } = new List<AssetAllocation>();
    // [ĐÃ ĐÓNG]
    // public ICollection<BudgetForecast> BudgetForecasts { get; set; } = new List<BudgetForecast>();
}
