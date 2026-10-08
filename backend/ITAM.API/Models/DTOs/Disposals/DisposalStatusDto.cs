namespace ITAM.API.Models.DTOs.Disposals;

public class DisposalStatusDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Color { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

public class UpsertDisposalStatusRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "slate";
    public int SortOrder { get; set; }
}
