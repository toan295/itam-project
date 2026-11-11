using System.Security.Claims;
using ITAM.API.Authorization;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.Backups;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ITAM.API.Controllers;

// Sao lưu & phục hồi dữ liệu: chỉ Admin IT (quyền hệ thống). Mọi thao tác ghi nhật ký (đối tượng "Backup").
[ApiController]
[Route("api/v1/backups")]
[HasPermission(Permissions.BackupManage)]
public class BackupsController : ControllerBase
{
    private const long MaxUploadBytes = 512L * 1024 * 1024;
    private const string RestoreConfirmText = "KHOI PHUC";

    private readonly IBackupService _service;

    public BackupsController(IBackupService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List() => Ok(ApiResponse<object>.Ok(await _service.ListAsync()));

    [HttpPost]
    public Task<IActionResult> Create() =>
        Run(async () => Ok(ApiResponse<object>.Ok(await _service.CreateAsync(UserId()), "Đã tạo bản sao lưu.")));

    [HttpGet("{name}/download")]
    public async Task<IActionResult> Download(string name)
    {
        try
        {
            var file = _service.GetFile(name);
            await _service.RecordDownloadAsync(UserId(), file.Name);   // file chứa dữ liệu nhạy cảm (mật khẩu đã mã hoá): mỗi lần tải đều có dấu vết
            return PhysicalFile(file.FullName, "application/sql", file.Name);
        }
        catch (BackupNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public Task<IActionResult> Upload(IFormFile? file) =>
        Run(async () =>
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Hãy chọn file sao lưu (.sql)."));
            }

            if (!file.FileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Fail("Chỉ nhận file .sql do chức năng sao lưu tạo ra."));
            }

            await using var stream = file.OpenReadStream();
            return Ok(ApiResponse<object>.Ok(await _service.SaveUploadAsync(UserId(), stream, file.Length), "Đã tải bản sao lưu lên."));
        });

    [HttpPost("{name}/restore")]
    public Task<IActionResult> Restore(string name, [FromBody] RestoreBackupRequestDto dto) =>
        Run(async () =>
        {
            if (!string.Equals(dto?.ConfirmText?.Trim(), RestoreConfirmText, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Fail($"Phải gõ \"{RestoreConfirmText}\" để xác nhận phục hồi."));
            }

            var result = await _service.RestoreAsync(UserId(), name);
            return Ok(ApiResponse<object>.Ok(result, "Đã phục hồi dữ liệu."));
        });

    [HttpDelete("{name}")]
    public Task<IActionResult> Delete(string name) =>
        Run(async () =>
        {
            await _service.DeleteAsync(UserId(), name);
            return NoContent();
        });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (BackupNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (BackupOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
