namespace ITAM.API.Models.DTOs.Lifecycle;

public class UpsertLifecyclePolicyRequestDto
{
    public int MaxAgeYears { get; set; }
    public int MaxFailureCount { get; set; }

    // Phiên bản cấu hình client đang thấy: null = "loại này đang dùng mặc định, tôi tạo ngưỡng riêng";
    // có giá trị = "tôi đang sửa đúng bản này". Không khớp trạng thái hiện tại -> 409.
    public int? Version { get; set; }
}
