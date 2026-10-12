namespace ITAM.API.Models.Entities;

// Định mức số thiết bị trên mỗi nhân sự theo loại tài sản (toàn hệ thống) — UC-17, E3.
// CategoryId vừa là PK vừa là FK → mỗi loại tối đa 1 định mức.
public class AssetCategoryDeviceRatio
{
    public int CategoryId { get; set; }
    public decimal DevicesPerPerson { get; set; }   // decimal(6,2), 0,01..20,00

    public AssetCategory Category { get; set; } = null!;
}
