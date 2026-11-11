namespace ITAM.API.Models.Entities;

public class AssetCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    // Xoá mềm: bấm "Xoá" trên giao diện chỉ đánh dấu dòng này, dữ liệu vẫn còn trong CSDL (và không hiện lại khi chạy lại hệ thống).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
