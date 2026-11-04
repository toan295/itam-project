using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FluentValidation;
using ITAM.API.Data;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.Entities;
using ITAM.API.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class AssetImportService
{
    public const long MaxFileSize = 10 * 1024 * 1024;
    private const int MaxRows = 5000;
    private const int MaxReportedErrors = 200;
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private readonly AppDbContext _db;
    private readonly IValidator<CreateAssetRequestDto> _validator;

    public AssetImportService(AppDbContext db, IValidator<CreateAssetRequestDto> validator)
    {
        _db = db;
        _validator = validator;
    }

    public static byte[] CreateTemplate()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(archive, "[Content_Types].xml", new XDocument(
                new XElement(ContentTypesNs + "Types",
                    new XElement(ContentTypesNs + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ContentTypesNs + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(ContentTypesNs + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ContentTypesNs + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            AddXml(archive, "_rels/.rels", new XDocument(
                new XElement(PackageRelNs + "Relationships",
                    new XElement(PackageRelNs + "Relationship", new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")))));
            AddXml(archive, "xl/workbook.xml", new XDocument(
                new XElement(MainNs + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", RelNs),
                    new XElement(MainNs + "sheets",
                        new XElement(MainNs + "sheet", new XAttribute("name", "TaiSan"), new XAttribute("sheetId", "1"), new XAttribute(RelNs + "id", "rId1"))))));
            AddXml(archive, "xl/_rels/workbook.xml.rels", new XDocument(
                new XElement(PackageRelNs + "Relationships",
                    new XElement(PackageRelNs + "Relationship", new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")))));

            var headers = new[] { "Mã tài sản", "Tên tài sản", "Loại tài sản", "Phòng ban", "Số serial", "Cấu hình", "Hệ điều hành", "Ngày mua", "Hạn bảo hành" };
            var headerRow = new XElement(MainNs + "row", new XAttribute("r", "1"),
                headers.Select((value, index) => InlineCell(index, 1, value)));
            var sample = new XElement(MainNs + "row", new XAttribute("r", "2"),
                new[] { "TS-001", "Laptop mẫu", "Máy tính", "IT", "SN-001", "RAM 16GB", "Windows 11", "2025-01-15", "2028-01-15" }
                    .Select((value, index) => InlineCell(index, 2, value)));
            AddXml(archive, "xl/worksheets/sheet1.xml", new XDocument(
                new XElement(MainNs + "worksheet", new XElement(MainNs + "sheetData", headerRow, sample))));
        }

        return stream.ToArray();
    }

    public async Task<AssetImportResultDto> ImportAsync(string fileName, Stream fileStream, int importedBy)
    {
        List<ParsedAssetRow> rows;
        try
        {
            rows = ReadRows(fileStream);
        }
        catch (InvalidDataException ex)
        {
            var failedAttempt = new ImportLog
            {
                FileName = SafeFileName(fileName), ImportedBy = importedBy,
                TotalRows = 0, SuccessRows = 0, FailedRows = 1,
            };
            _db.ImportLogs.Add(failedAttempt);
            await _db.SaveChangesAsync();
            return new AssetImportResultDto
            {
                FailedRows = 1,
                Errors = new[] { new AssetImportRowErrorDto { Row = 0, Message = ex.Message } },
            };
        }

        if (rows.Count == 0)
        {
            _db.ImportLogs.Add(new ImportLog
            {
                FileName = SafeFileName(fileName), ImportedBy = importedBy,
                TotalRows = 0, SuccessRows = 0, FailedRows = 1,
            });
            await _db.SaveChangesAsync();
            return new AssetImportResultDto
            {
                FailedRows = 1,
                Errors = new[] { new AssetImportRowErrorDto { Row = 0, Message = "File không có dòng dữ liệu để import." } },
            };
        }

        var errors = new List<AssetImportRowErrorDto>();
        if (rows.Count > MaxRows)
        {
            _db.ImportLogs.Add(new ImportLog
            {
                FileName = SafeFileName(fileName), ImportedBy = importedBy,
                TotalRows = rows.Count, SuccessRows = 0, FailedRows = rows.Count,
            });
            await _db.SaveChangesAsync();
            return new AssetImportResultDto
            {
                TotalRows = rows.Count,
                FailedRows = rows.Count,
                Errors = new[] { new AssetImportRowErrorDto { Row = 0, Message = $"File vượt quá giới hạn {MaxRows} dòng dữ liệu; không có dòng nào được nhập." } },
            };
        }

        var categories = await _db.AssetCategories.AsNoTracking().ToListAsync();
        var departments = await _db.Departments.AsNoTracking().ToListAsync();
        var existingCodes = await _db.Assets.AsNoTracking().Select(asset => asset.AssetCode).ToListAsync();
        var existingSerials = await _db.Assets.AsNoTracking().Where(asset => asset.SerialNumber != null)
            .Select(asset => asset.SerialNumber!).ToListAsync();
        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var serialSet = existingSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validAssets = new List<Asset>();
        var categoryMap = categories.ToDictionary(category => Normalize(category.Name), category => category);
        var departmentMap = departments.ToDictionary(department => Normalize(department.Name), department => department);

        foreach (var row in rows)
        {
            var rowErrors = row.ParseErrors.ToList();
            var categoryFound = categoryMap.TryGetValue(Normalize(row.CategoryName), out var category);
            var departmentFound = departmentMap.TryGetValue(Normalize(row.DepartmentName), out var department);
            if (!categoryFound) rowErrors.Add($"Không tìm thấy loại tài sản '{row.CategoryName}'.");
            if (!departmentFound) rowErrors.Add($"Không tìm thấy phòng ban '{row.DepartmentName}'.");

            var dto = new CreateAssetRequestDto
            {
                AssetCode = row.AssetCode,
                Name = row.Name,
                CategoryId = category?.Id ?? 0,
                DepartmentId = department?.Id ?? 0,
                SerialNumber = row.SerialNumber,
                Specification = row.Specification,
                OperatingSystem = row.OperatingSystem,
                PurchaseDate = row.PurchaseDate,
                WarrantyExpiry = row.WarrantyExpiry,
            };
            var validation = await _validator.ValidateAsync(dto);
            rowErrors.AddRange(validation.Errors.Select(error => error.ErrorMessage));

            var code = dto.AssetCode?.Trim() ?? string.Empty;
            if (code.Length > 0 && codeSet.Contains(code)) rowErrors.Add($"Mã tài sản '{code}' bị trùng trong hệ thống hoặc file.");
            var serial = string.IsNullOrWhiteSpace(dto.SerialNumber) ? null : dto.SerialNumber.Trim();
            if (serial != null && serialSet.Contains(serial)) rowErrors.Add($"Số serial '{serial}' bị trùng trong hệ thống hoặc file.");

            if (rowErrors.Count > 0)
            {
                if (errors.Count < MaxReportedErrors)
                    errors.Add(new AssetImportRowErrorDto { Row = row.RowNumber, Message = string.Join(" ", rowErrors.Distinct()) });
                continue;
            }

            codeSet.Add(code);
            if (serial != null) serialSet.Add(serial);

            validAssets.Add(new Asset
            {
                AssetCode = code,
                Name = dto.Name.Trim(),
                CategoryId = category!.Id,
                DepartmentId = department!.Id,
                SerialNumber = serial,
                Specification = string.IsNullOrWhiteSpace(dto.Specification) ? null : dto.Specification.Trim(),
                OperatingSystem = string.IsNullOrWhiteSpace(dto.OperatingSystem) ? null : dto.OperatingSystem.Trim(),
                PurchaseDate = dto.PurchaseDate,
                WarrantyExpiry = dto.WarrantyExpiry,
                Status = AssetStatus.InUse,
                CreatedAt = DateTime.UtcNow,
            });
        }

        var totalRows = rows.Count;
        var failedRows = totalRows - validAssets.Count;
        var importLog = new ImportLog
        {
            FileName = SafeFileName(fileName), ImportedBy = importedBy,
            TotalRows = totalRows, SuccessRows = validAssets.Count, FailedRows = failedRows,
        };

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            _db.Assets.AddRange(validAssets);
            _db.ImportLogs.Add(importLog);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _db.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            importLog = new ImportLog
            {
                FileName = SafeFileName(fileName), ImportedBy = importedBy,
                TotalRows = totalRows, SuccessRows = 0, FailedRows = totalRows,
            };
            _db.ImportLogs.Add(importLog);
            await _db.SaveChangesAsync();
            if (errors.Count < MaxReportedErrors)
                errors.Add(new AssetImportRowErrorDto { Row = 0, Message = "Dữ liệu bị trùng hoặc thay đổi trong lúc nhập; không có dòng nào được ghi." });
            return new AssetImportResultDto { TotalRows = totalRows, FailedRows = totalRows, Errors = errors };
        }

        return new AssetImportResultDto
        {
            TotalRows = totalRows,
            SuccessRows = validAssets.Count,
            FailedRows = failedRows,
            Errors = errors,
        };
    }

    private static List<ParsedAssetRow> ReadRows(Stream fileStream)
    {
        try
        {
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
            var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("File không phải Excel .xlsx hợp lệ.");
            var workbook = LoadXml(workbookEntry);
            var sheet = workbook.Root?.Element(MainNs + "sheets")?.Elements(MainNs + "sheet").FirstOrDefault()
                ?? throw new InvalidDataException("Workbook không có sheet nào.");
            var relId = (string?)sheet.Attribute(RelNs + "id") ?? throw new InvalidDataException("Không đọc được sheet đầu tiên.");
            var relationships = LoadXml(archive.GetEntry("xl/_rels/workbook.xml.rels")
                ?? throw new InvalidDataException("Workbook thiếu thông tin sheet."));
            var target = relationships.Root?.Elements(PackageRelNs + "Relationship")
                .FirstOrDefault(relation => (string?)relation.Attribute("Id") == relId)?.Attribute("Target")?.Value
                ?? throw new InvalidDataException("Không tìm thấy sheet dữ liệu.");
            var sheetPath = target.StartsWith("/", StringComparison.Ordinal)
                ? target.TrimStart('/')
                : target.StartsWith("xl/", StringComparison.Ordinal) ? target : "xl/" + target;
            var sheetEntry = archive.GetEntry(sheetPath) ?? throw new InvalidDataException("Không đọc được sheet dữ liệu.");
            var sheetDoc = LoadXml(sheetEntry);
            var sharedStrings = ReadSharedStrings(archive.GetEntry("xl/sharedStrings.xml"));
            var rows = sheetDoc.Root?.Element(MainNs + "sheetData")?.Elements(MainNs + "row").ToList()
                ?? throw new InvalidDataException("Sheet không có dữ liệu.");
            if (rows.Count == 0) throw new InvalidDataException("File không có dòng tiêu đề.");

            var headerRow = ReadCells(rows[0], sharedStrings);
            var columns = headerRow.ToDictionary(pair => NormalizeHeader(pair.Value), pair => pair.Key);
            // NormalizeHeader bỏ dấu và ký tự phân cách, ví dụ "Mã tài sản" thành "mataisan".
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mataisan"] = "assetCode", ["assetcode"] = "assetCode", ["ma"] = "assetCode",
                ["tentaisan"] = "name", ["name"] = "name", ["ten"] = "name",
                ["loaitaisan"] = "category", ["category"] = "category", ["loai"] = "category",
                ["phongban"] = "department", ["department"] = "department",
                ["soserial"] = "serial", ["serialnumber"] = "serial", ["serial"] = "serial",
                ["cauhinh"] = "specification", ["specification"] = "specification",
                ["hedieuhanh"] = "operatingSystem", ["operatingsystem"] = "operatingSystem",
                ["ngaymua"] = "purchaseDate", ["purchasedate"] = "purchaseDate",
                ["hanbaohanh"] = "warrantyExpiry", ["warrantyexpiry"] = "warrantyExpiry",
            };
            var mapped = columns.Where(pair => aliases.ContainsKey(pair.Key))
                .ToDictionary(pair => aliases[pair.Key], pair => pair.Value, StringComparer.Ordinal);
            var missing = new[] { "assetCode", "name", "category", "department" }.Where(key => !mapped.ContainsKey(key)).ToList();
            if (missing.Count > 0)
                throw new InvalidDataException("Thiếu cột bắt buộc: " + string.Join(", ", missing.Select(HeaderLabel)) + ". Tải file mẫu để xem định dạng.");

            var result = new List<ParsedAssetRow>();
            for (var index = 1; index < rows.Count; index++)
            {
                var values = ReadCells(rows[index], sharedStrings);
                if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
                var rowNumber = int.TryParse((string?)rows[index].Attribute("r"), out var parsed) ? parsed : index + 1;
                var parseErrors = new List<string>();
                var purchaseDate = ParseDate(Get(values, mapped, "purchaseDate"), rowNumber, "Ngày mua", parseErrors);
                var warrantyExpiry = ParseDate(Get(values, mapped, "warrantyExpiry"), rowNumber, "Hạn bảo hành", parseErrors);
                result.Add(new ParsedAssetRow
                {
                    RowNumber = rowNumber,
                    AssetCode = Get(values, mapped, "assetCode"),
                    Name = Get(values, mapped, "name"),
                    CategoryName = Get(values, mapped, "category"),
                    DepartmentName = Get(values, mapped, "department"),
                    SerialNumber = Get(values, mapped, "serial"),
                    Specification = Get(values, mapped, "specification"),
                    OperatingSystem = Get(values, mapped, "operatingSystem"),
                    PurchaseDate = purchaseDate,
                    WarrantyExpiry = warrantyExpiry,
                    ParseErrors = parseErrors,
                });
            }
            return result;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is IOException or XmlException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Không thể đọc file Excel. Hãy dùng định dạng .xlsx và tải file mẫu.", ex);
        }
    }

    private static Dictionary<int, string> ReadCells(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var result = new Dictionary<int, string>();
        foreach (var cell in row.Elements(MainNs + "c"))
        {
            var reference = (string?)cell.Attribute("r") ?? string.Empty;
            var columnIndex = ColumnIndex(reference);
            var type = (string?)cell.Attribute("t");
            var value = type == "inlineStr"
                ? string.Concat(cell.Descendants(MainNs + "t").Select(element => element.Value))
                : cell.Element(MainNs + "v")?.Value ?? string.Empty;
            if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                value = sharedStrings[sharedIndex];
            result[columnIndex] = value.Trim();
        }
        return result;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchiveEntry? entry)
    {
        if (entry == null) return Array.Empty<string>();
        return LoadXml(entry).Root?.Elements(MainNs + "si")
            .Select(item => string.Concat(item.Descendants(MainNs + "t").Select(text => text.Value))).ToList()
            ?? new List<string>();
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("Thành phần trong file Excel vượt quá giới hạn kích thước.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 20 * 1024 * 1024,
        });
        return XDocument.Load(reader);
    }

    private static DateOnly? ParseDate(string value, int row, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial >= 1 && serial <= 2_958_465)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy" };
        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        errors.Add($"{label} '{value}' không đúng định dạng ngày. Dùng yyyy-MM-dd hoặc dd/MM/yyyy.");
        return null;
    }

    private static string Get(IReadOnlyDictionary<int, string> values, IReadOnlyDictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var index) && values.TryGetValue(index, out var value) ? value : string.Empty;

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var ch in cellReference.TakeWhile(char.IsLetter)) index = index * 26 + char.ToUpperInvariant(ch) - 'A' + 1;
        return Math.Max(index - 1, 0);
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        return builder.ToString();
    }

    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    private static string HeaderLabel(string key) => key switch
    {
        "assetCode" => "Mã tài sản", "name" => "Tên tài sản", "category" => "Loại tài sản", "department" => "Phòng ban", _ => key,
    };
    private static string SafeFileName(string name) => Path.GetFileName(name).Length <= 255 ? Path.GetFileName(name) : Path.GetFileName(name)[..255];

    private static XElement InlineCell(int columnIndex, int rowIndex, string value)
    {
        var columnName = string.Empty;
        var number = columnIndex + 1;
        while (number > 0)
        {
            number--;
            columnName = (char)('A' + number % 26) + columnName;
            number /= 26;
        }
        return new XElement(MainNs + "c", new XAttribute("r", $"{columnName}{rowIndex}"), new XAttribute("t", "inlineStr"),
            new XElement(MainNs + "is", new XElement(MainNs + "t", value)));
    }

    private static void AddXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true });
        document.Save(writer);
    }

    private sealed class ParsedAssetRow
    {
        public int RowNumber { get; init; }
        public string AssetCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string DepartmentName { get; init; } = string.Empty;
        public string? SerialNumber { get; init; }
        public string? Specification { get; init; }
        public string? OperatingSystem { get; init; }
        public DateOnly? PurchaseDate { get; init; }
        public DateOnly? WarrantyExpiry { get; init; }
        public IReadOnlyList<string> ParseErrors { get; init; } = Array.Empty<string>();
    }
}
