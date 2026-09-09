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

    public User ImportedByUser { get; set; } = null!;
}
