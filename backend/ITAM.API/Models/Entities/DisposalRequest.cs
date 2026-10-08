namespace ITAM.API.Models.Entities;

// Phiếu thanh lý: Technician kiểm tra -> đề xuất -> Manager duyệt/từ chối -> Admin IT thực hiện -> hoàn tất.
public class DisposalRequest
{
    public int Id { get; set; }
    public int AssetId { get; set; }

    // Bước hiện tại của luồng (luôn là trạng thái hệ thống) và trạng thái phụ tuỳ chọn (trạng thái do Admin thêm).
    public int StatusId { get; set; }
    public int? SubStatusId { get; set; }

    // Bước 1: kiểm tra
    public string InspectionNote { get; set; } = null!;
    public int InspectedByUserId { get; set; }
    public DateTime InspectedAt { get; set; } = DateTime.UtcNow;

    // Bước 2: đề xuất
    public string? Reason { get; set; }
    public string? DisposalMethod { get; set; }
    public int? ProposedByUserId { get; set; }
    public DateTime? ProposedAt { get; set; }

    // Bước 3: Manager duyệt / từ chối
    public string? ReviewNote { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Bước 4: Admin IT thực hiện
    public string? CompletionNote { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Asset Asset { get; set; } = null!;
    public DisposalStatus Status { get; set; } = null!;
    public DisposalStatus? SubStatus { get; set; }
    public User InspectedBy { get; set; } = null!;
    public User? ProposedBy { get; set; }
    public User? ReviewedBy { get; set; }
    public User? CompletedBy { get; set; }
}
