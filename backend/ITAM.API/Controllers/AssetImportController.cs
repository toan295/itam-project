using System.Security.Claims;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

[ApiController]
[Route("api/v1/assets/import")]
[Authorize]
public class AssetImportController : ControllerBase
{
    private readonly AssetImportService _service;

    public AssetImportController(AssetImportService service) => _service = service;

    [HttpGet("template")]
    public IActionResult DownloadTemplate() => File(
        AssetImportService.CreateTemplate(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "mau-import-tai-san.xlsx");

    [HttpPost]
    [Authorize(Roles = "Admin IT")]
    [RequestSizeLimit(AssetImportService.MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = AssetImportService.MaxFileSize)]
    public async Task<IActionResult> Import([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn file Excel .xlsx."));
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("Chỉ hỗ trợ file Excel định dạng .xlsx."));
        if (file.Length > AssetImportService.MaxFileSize)
            return BadRequest(ApiResponse<object>.Fail("Dung lượng file tối đa là 10 MB."));
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Không xác định được người nhập."));

        await using var stream = file.OpenReadStream();
        var result = await _service.ImportAsync(file.FileName, stream, userId);
        return Ok(ApiResponse<AssetImportResultDto>.Ok(result, "Đã xử lý file import tài sản."));
    }
}
