namespace ITAM.API.Models.DTOs.AuditLogs;

public class AuditLogResponseDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    /// <summary>Họ tên người thực hiện TẠI THỜI ĐIỂM đó (bản chụp); bản ghi cũ chưa có thì lấy tên hiện tại.</summary>
    public string UserName { get; set; } = string.Empty;
    public string? ActorRole { get; set; }
    /// <summary>Email hiện tại của tài khoản. Chỉ người giữ quyền audit-logs.view (Admin IT) gọi được API này; null nếu tài khoản đã bị xoá.</summary>
    public string? UserEmail { get; set; }
    public bool UserIsDeleted { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    /// <summary>Success | Rejected | Failed — xem <see cref="Models.Entities.AuditLog"/>.</summary>
    public string Outcome { get; set; } = "Success";
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>Yêu cầu người dùng đã gửi (đã che mật khẩu/token).</summary>
    public string? Detail { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    /// <summary>Chỉ có ở API chi tiết: Valid | Tampered | Unsigned (bản ghi cũ trước khi có chữ ký).</summary>
    public string? Integrity { get; set; }
}

public class AuditActorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsDeleted { get; set; }
    public string? Role { get; set; }
    public int EventCount { get; set; }
}

public class AuditCatalogItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>false: mã có trong dữ liệu nhưng không thuộc danh mục hiện hành (dữ liệu cũ).</summary>
    public bool Known { get; set; } = true;
}

public class AuditCatalogDto
{
    public List<AuditCatalogItemDto> Entities { get; set; } = new();
    public List<AuditCatalogItemDto> Actions { get; set; } = new();
    public List<AuditCatalogItemDto> Outcomes { get; set; } = new();
}

public class AuditIntegrityReportDto
{
    public long Checked { get; set; }
    public long Valid { get; set; }
    public long Unsigned { get; set; }
    public List<long> TamperedIds { get; set; } = new();
    /// <summary>Trigger chặn sửa/xoá còn tồn tại trong database không.</summary>
    public bool ImmutabilityTriggersPresent { get; set; }
    public bool Truncated { get; set; }
}
