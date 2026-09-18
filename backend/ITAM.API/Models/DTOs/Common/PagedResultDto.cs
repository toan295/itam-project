namespace ITAM.API.Models.DTOs.Common;

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }

    // Tính tự động từ PageSize/TotalItems — tránh trường hợp quên gán/gán sai khi 2 Service khác nhau
    // (AssetService, SoftwareLicenseService) cùng dựng PagedResultDto.
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}
