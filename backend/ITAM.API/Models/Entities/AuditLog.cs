namespace ITAM.API.Models.Entities;

/// <summary>
/// Một sự kiện đã xảy ra, KHÔNG BAO GIỜ được sửa hay xoá (trigger của DB chặn UPDATE/DELETE; RowHash phát hiện sửa trực tiếp).
/// Outcome cho biết kết quả cuối cùng: Success (nghiệp vụ đã thực hiện), Rejected (bị từ chối: thiếu quyền, dữ liệu sai, xung đột,
/// không tìm thấy — không có thay đổi dữ liệu), Failed (lỗi hệ thống, thao tác không hoàn tất / bị hoàn lại).
/// OldValue/NewValue là thay đổi dữ liệu THỰC SỰ (chỉ các trường đổi); yêu cầu người dùng gửi lên nằm ở Detail.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public int EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Outcome { get; set; } = AuditOutcome.Success;
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public string? Detail { get; set; }
    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }
    /// <summary>Họ tên và vai trò của người thực hiện TẠI THỜI ĐIỂM đó (hồ sơ có thể đổi tên/vai trò hay bị xoá sau này).</summary>
    public string? ActorName { get; set; }
    public string? ActorRole { get; set; }
    /// <summary>HMAC-SHA256 của nội dung bản ghi; null ở bản ghi cũ trước khi có chữ ký.</summary>
    public string? RowHash { get; set; }

    public User User { get; set; } = null!;
}

public static class AuditOutcome
{
    public const string Success = "Success";
    public const string Rejected = "Rejected";
    public const string Failed = "Failed";

    public static readonly IReadOnlyList<string> All = new[] { Success, Rejected, Failed };
}
