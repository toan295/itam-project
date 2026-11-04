namespace ITAM.API.Models.DTOs.Assets;

public class AssetImportRowErrorDto
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AssetImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public IReadOnlyList<AssetImportRowErrorDto> Errors { get; set; } = Array.Empty<AssetImportRowErrorDto>();
}
