namespace ITAM.API.Models.DTOs.Disposals;

public class CreateDisposalRequestDto
{
    public int AssetId { get; set; }
    public string InspectionNote { get; set; } = string.Empty;
}

public class ProposeDisposalRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? DisposalMethod { get; set; }
}

// Dùng cho cả duyệt (ghi chú tuỳ chọn) và từ chối (ghi chú bắt buộc — Service kiểm tra).
public class ReviewDisposalRequestDto
{
    public string? Note { get; set; }
}

public class CompleteDisposalRequestDto
{
    public string? Note { get; set; }
}

public class SetDisposalSubStatusRequestDto
{
    public int? SubStatusId { get; set; }
}

public class DisposalCandidateDto
{
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = default!;
    public string AssetName { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public string? SerialNumber { get; set; }
}

public class DisposalRequestResponseDto
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string AssetCode { get; set; } = default!;
    public string AssetName { get; set; } = default!;
    public string AssetStatus { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;

    public int StatusId { get; set; }
    public string StatusCode { get; set; } = default!;
    public string StatusName { get; set; } = default!;
    public string StatusColor { get; set; } = default!;
    public int? SubStatusId { get; set; }
    public string? SubStatusName { get; set; }
    public string? SubStatusColor { get; set; }

    public string InspectionNote { get; set; } = default!;
    public string InspectedByName { get; set; } = default!;
    public DateTime InspectedAt { get; set; }

    public string? Reason { get; set; }
    public string? DisposalMethod { get; set; }
    public string? ProposedByName { get; set; }
    public DateTime? ProposedAt { get; set; }

    public string? ReviewNote { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public string? CompletionNote { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
