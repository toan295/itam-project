namespace ITAM.API.Models.Entities;

public class ImportLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public int ImportedBy { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public string Mode { get; set; } = "skip-errors";   // "skip-errors" (ghi dòng hợp lệ, bỏ dòng lỗi) | "all-or-nothing"
    public string? FileSha256 { get; set; }             // phát hiện import lại đúng file đã dùng
    public string? ErrorsJson { get; set; }             // các dòng bị bỏ qua và lý do (tối đa 500 dòng đầu)

    public User ImportedByUser { get; set; } = null!;
}
