namespace ITAM.API.Repositories.Models;

// Bộ lọc bổ sung cho danh sách phiếu thanh lý (Service đã validate). CreatedTo là cận trên LOẠI TRỪ.
// Sort: newest (mặc định) | oldest.
public class DisposalListExtras
{
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public string? Sort { get; set; }
}
