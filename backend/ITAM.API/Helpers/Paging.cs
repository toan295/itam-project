namespace ITAM.API.Helpers;

// pageSize ngoài [1, 100] quay về mặc định 20 (đã là quy ước của hầu hết module và được test khoá lại).
// Chuẩn hoá tham số phân trang cho MỌI endpoint danh sách (Mục 4: page mặc định 1, pageSize mặc định 20, tối đa 100).
// Phải chặn cả page tối đa: (page - 1) * pageSize là phép nhân int, page = int.MaxValue sẽ tràn số và làm
// Skip() ném lỗi -> 500 ở mọi API danh sách.
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 1_000_000; // MaxPage * MaxPageSize = 10^8, luôn nằm trong phạm vi int.

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Clamp(page, 1, MaxPage), pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize);
}
