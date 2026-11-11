using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ITAM.API.Configurations;
using ITAM.API.Data;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Backups;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ITAM.API.Services.Implementations;

/// <summary>
/// Sao lưu / phục hồi toàn bộ cơ sở dữ liệu bằng mysqldump / mysql (file .sql nằm trong thư mục sao lưu của máy chủ).
/// Mật khẩu DB chỉ truyền qua biến môi trường của tiến trình con, không xuất hiện trong dòng lệnh.
/// </summary>
public class BackupService : IBackupService
{
    private const string FileNamePattern = @"^eaims-(?:sao-luu-)?\d{8}-\d{6}(-[a-z]+)?\.sql$";
    private static readonly Regex NameRegex = new(FileNamePattern, RegexOptions.Compiled);

    // Chỉ một thao tác sao lưu/phục hồi tại một thời điểm (một máy chủ API).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly BackupSettings _settings;
    private readonly string _contentRoot;
    private readonly ILogger<BackupService> _logger;
    private readonly MySqlConnectionStringBuilder _connection;

    public BackupService(
        AppDbContext db, IAuditLogService audit, IOptions<BackupSettings> settings,
        IConfiguration configuration, IHostEnvironment environment, ILogger<BackupService> logger)
    {
        _db = db;
        _audit = audit;
        _settings = settings.Value;
        _contentRoot = environment.ContentRootPath;
        _logger = logger;
        _connection = new MySqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
    }

    private string Directory
    {
        get
        {
            var path = Path.IsPathRooted(_settings.Directory) ? _settings.Directory : Path.Combine(_contentRoot, _settings.Directory);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }
    }

    public Task<IReadOnlyList<BackupFileDto>> ListAsync()
    {
        var items = new DirectoryInfo(Directory).GetFiles("*.sql")
            .Where(f => NameRegex.IsMatch(f.Name))
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
        return Task.FromResult<IReadOnlyList<BackupFileDto>>(items);
    }

    public async Task<BackupFileDto> CreateAsync(int userId) => await CreateCoreAsync(userId, null, audit: true);

    private async Task<BackupFileDto> CreateCoreAsync(int userId, string? tag, bool audit)
    {
        await Gate.WaitAsync();
        try
        {
            return await DumpAsync(userId, tag, audit);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<BackupFileDto> DumpAsync(int userId, string? tag, bool audit)
    {
        var tool = FindTool("mysqldump");
        var name = ExportFileNames.Build(ExportFileNames.Backup, "sql", tag: tag);
        var finalPath = Path.Combine(Directory, name);
        var tempPath = finalPath + ".tmp";

        var args = new List<string>
        {
            "--single-transaction", "--routines", "--triggers", "--events", "--no-tablespaces", "--hex-blob",
            "--default-character-set=utf8mb4",
        };
        AddConnectionArgs(args);
        args.Add(_connection.Database);

        try
        {
            await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var (code, error) = await RunAsync(tool, args, async process =>
                {
                    await process.StandardOutput.BaseStream.CopyToAsync(output);
                });
                if (code != 0)
                {
                    throw new BackupOperationException($"Sao lưu thất bại: {Summarize(error)}");
                }
            }

            if (new FileInfo(tempPath).Length < 200)
            {
                throw new BackupOperationException("Sao lưu thất bại: file sao lưu rỗng.");
            }

            File.Move(tempPath, finalPath, overwrite: false);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        var dto = ToDto(new FileInfo(finalPath));
        if (audit)
        {
            await TryAuditAsync(userId, "Backup", new { file = dto.Name, sizeBytes = dto.SizeBytes });
        }

        return dto;
    }

    public FileInfo GetFile(string name) => Resolve(name);

    public async Task<BackupFileDto> SaveUploadAsync(int userId, Stream content, long length)
    {
        if (length <= 0)
        {
            throw new BackupOperationException("File rỗng.");
        }

        await Gate.WaitAsync();
        try
        {
            var name = ExportFileNames.Build(ExportFileNames.Backup, "sql", tag: "uploaded");
            var finalPath = Path.Combine(Directory, name);
            var tempPath = finalPath + ".tmp";
            try
            {
                await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await content.CopyToAsync(output);
                }

                EnsureLooksLikeDump(tempPath);
                File.Move(tempPath, finalPath, overwrite: false);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            var dto = ToDto(new FileInfo(finalPath));
            await TryAuditAsync(userId, "Upload", new { file = dto.Name, sizeBytes = dto.SizeBytes });
            return dto;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task DeleteAsync(int userId, string name)
    {
        await Gate.WaitAsync();
        try
        {
            var file = Resolve(name);
            file.Delete();
            await TryAuditAsync(userId, "Delete", new { file = name });
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<RestoreResultDto> RestoreAsync(int userId, string name)
    {
        var file = Resolve(name);
        EnsureLooksLikeDump(file.FullName);

        await Gate.WaitAsync();
        try
        {
            // Luôn tạo bản sao lưu an toàn của dữ liệu hiện tại trước khi ghi đè — để quay lại được nếu phục hồi nhầm.
            var safety = await DumpAsync(userId, "truockhoiphuc", audit: false);
            var mysql = FindTool("mysql");
            var args = new List<string> { "--default-character-set=utf8mb4", "--max-allowed-packet=256M" };
            AddConnectionArgs(args);
            args.Add(_connection.Database);

            var (code, error) = await RunAsync(mysql, args, async process =>
            {
                await using var input = file.OpenRead();
                await input.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            });

            if (code != 0)
            {
                _logger.LogError("Phục hồi thất bại ({Code}): {Error}", code, error);
                // Cố khôi phục lại dữ liệu cũ để hệ thống không nằm ở trạng thái nửa vời.
                var rolledBack = await TryRollbackAsync(safety);
                throw new BackupOperationException(
                    $"Phục hồi thất bại: {Summarize(error)} " +
                    (rolledBack ? "Dữ liệu hiện tại đã được giữ nguyên." : $"Không tự quay lại được — hãy phục hồi thủ công từ {safety.Name}."));
            }

            // Kết nối cũ trỏ vào cấu trúc cũ: bỏ ra, nâng cấp cấu trúc nếu bản sao lưu cũ hơn, và bù quyền hệ thống cho Admin IT.
            MySqlConnection.ClearAllPools();
            await _db.Database.MigrateAsync();
            await PermissionSeeder.EnsureAsync(_db, _logger);

            await TryAuditAsync(userId, "Restore", new { file = file.Name, safetyBackup = safety.Name });
            return new RestoreResultDto { RestoredFrom = file.Name, SafetyBackup = safety.Name };
        }
        finally
        {
            Gate.Release();
        }
    }

    // ---------- helpers ----------

    private async Task<bool> TryRollbackAsync(BackupFileDto safety)
    {
        try
        {
            var args = new List<string> { "--default-character-set=utf8mb4", "--max-allowed-packet=256M" };
            AddConnectionArgs(args);
            args.Add(_connection.Database);
            var path = Path.Combine(Directory, safety.Name);
            var (code, _) = await RunAsync(FindTool("mysql"), args, async process =>
            {
                await using var input = File.OpenRead(path);
                await input.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            });
            return code == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không quay lại được dữ liệu cũ sau khi phục hồi thất bại.");
            return false;
        }
    }

    private FileInfo Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !NameRegex.IsMatch(name))
        {
            throw new BackupNotFoundException(name);
        }

        var file = new FileInfo(Path.Combine(Directory, name));
        if (!file.Exists)
        {
            throw new BackupNotFoundException(name);
        }

        return file;
    }

    // Chỉ nhận file do mysqldump tạo ra (có tiêu đề "MySQL dump" và lệnh CREATE TABLE) — chặn việc chạy SQL tuỳ ý dạng khác.
    private static void EnsureLooksLikeDump(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var buffer = new char[8192];
        var read = reader.Read(buffer, 0, buffer.Length);
        var head = new string(buffer, 0, read);
        if (!head.Contains("MySQL dump", StringComparison.Ordinal) && !head.Contains("mysqldump", StringComparison.OrdinalIgnoreCase))
        {
            throw new BackupOperationException("File không phải bản sao lưu của hệ thống (thiếu tiêu đề mysqldump).");
        }

        var hasTables = false;
        string? line;
        var scanned = 0;
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        reader.DiscardBufferedData();
        while ((line = reader.ReadLine()) is not null && scanned++ < 200000)
        {
            if (line.StartsWith("CREATE TABLE", StringComparison.Ordinal))
            {
                hasTables = true;
                break;
            }
        }

        if (!hasTables)
        {
            throw new BackupOperationException("File sao lưu không chứa bảng dữ liệu nào.");
        }
    }

    private void AddConnectionArgs(List<string> args)
    {
        args.Add($"--host={_connection.Server}");
        args.Add($"--port={_connection.Port}");
        args.Add($"--user={_connection.UserID}");
    }

    private async Task<(int ExitCode, string Error)> RunAsync(string tool, IEnumerable<string> args, Func<Process, Task> io)
    {
        var info = new ProcessStartInfo(tool)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
        {
            info.ArgumentList.Add(a);
        }

        info.Environment["MYSQL_PWD"] = _connection.Password;

        using var process = new Process { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new BackupOperationException($"Không chạy được công cụ {Path.GetFileName(tool)} ({ex.Message}). Kiểm tra cấu hình Backup:MysqlBinPath.");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, _settings.TimeoutSeconds)));
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await io(process);
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* đã thoát */ }
            throw new BackupOperationException("Thao tác quá thời gian cho phép và đã bị dừng.");
        }

        return (process.ExitCode, await errorTask);
    }

    private string FindTool(string tool)
    {
        var exe = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.MysqlBinPath))
        {
            candidates.Add(_settings.MysqlBinPath);
        }

        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        if (OperatingSystem.IsWindows())
        {
            foreach (var version in new[] { "8.0", "8.4", "9.0", "9.1", "9.2", "5.7" })
            {
                candidates.Add($@"C:\Program Files\MySQL\MySQL Server {version}\bin");
            }
        }

        foreach (var dir in candidates)
        {
            try
            {
                var path = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (ArgumentException)
            {
                // Mục PATH không hợp lệ — bỏ qua.
            }
        }

        throw new BackupOperationException(
            $"Không tìm thấy công cụ {exe} của MySQL trên máy chủ. Hãy cài MySQL Client hoặc khai báo thư mục chứa nó ở Backup:MysqlBinPath trong appsettings.");
    }

    public Task RecordDownloadAsync(int userId, string name) => TryAuditAsync(userId, "Download", new { file = name });

    private async Task TryAuditAsync(int userId, string action, object detail)
    {
        try
        {
            await _audit.RecordAsync(userId, action, "Backup", 0, null, detail);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Sau phục hồi bản sao lưu cũ, người thực hiện có thể không còn tồn tại trong dữ liệu — không để việc ghi nhật ký làm hỏng kết quả.
            _logger.LogWarning(ex, "Không ghi được nhật ký cho thao tác {Action} sao lưu.", action);
            _db.ChangeTracker.Clear();
        }
    }

    private static string Summarize(string error)
    {
        var text = string.IsNullOrWhiteSpace(error) ? "không rõ nguyên nhân" : error.Trim();
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length > 300 ? text[..300] + "…" : text;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* bỏ qua */ }
    }

    private static BackupFileDto ToDto(FileInfo f)
    {
        var match = Regex.Match(f.Name, @"^eaims-(?:sao-luu-)?(\d{8}-\d{6})(?:-([a-z]+))?\.sql$");
        var kind = match.Groups[2].Value switch
        {
            "truockhoiphuc" => "PreRestore",
            "uploaded" => "Uploaded",
            _ => "Manual",
        };
        var created = DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? local
            : f.LastWriteTime;
        return new BackupFileDto { Name = f.Name, SizeBytes = f.Length, CreatedAt = created, Kind = kind };
    }
}
