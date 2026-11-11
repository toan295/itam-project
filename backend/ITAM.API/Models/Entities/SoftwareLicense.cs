namespace ITAM.API.Models.Entities;

public class SoftwareLicense
{
    public int Id { get; set; }
    public string SoftwareName { get; set; } = null!;
    public string LicenseKey { get; set; } = null!;
    public DateOnly ExpiryDate { get; set; }
    public int MaxUsage { get; set; }
    public string? Notes { get; set; }

    // Xoá mềm: bấm "Xoá" trên giao diện chỉ đánh dấu dòng này, dữ liệu vẫn còn trong CSDL (và không hiện lại khi chạy lại hệ thống).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<AssetSoftwareLicense> AssetSoftwareLicenses { get; set; } = new List<AssetSoftwareLicense>();
}
