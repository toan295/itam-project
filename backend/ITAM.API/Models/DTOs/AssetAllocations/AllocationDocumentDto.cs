namespace ITAM.API.Models.DTOs.AssetAllocations;

// Dữ liệu có cấu trúc để in "Biên bản bàn giao/thu hồi tài sản" theo mẫu: bên giao, bên nhận, bảng tài sản.
public class AllocationDocumentDto
{
    public string Kind { get; set; } = "Handover"; // "Handover" (bàn giao) | "Return" (thu hồi)
    public int AllocationId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string? Location { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }

    public DocumentPartyDto Giver { get; set; } = new();     // Bên giao
    public DocumentPartyDto Receiver { get; set; } = new();  // Bên nhận

    public DocumentAssetLineDto Asset { get; set; } = new();
}

public class DocumentPartyDto
{
    public string FullName { get; set; } = "";
    public string? Position { get; set; }       // Chức danh
    public string? DepartmentName { get; set; } // Bộ phận
}

public class DocumentAssetLineDto
{
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string Unit { get; set; } = "Cái";
    public int Quantity { get; set; } = 1;
    public string? Condition { get; set; }      // Tình trạng
    public decimal? Amount { get; set; }        // Thành tiền (VND) theo đơn giá tham khảo của loại tài sản; null nếu chưa cấu hình.
}
