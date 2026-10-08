namespace ITAM.API.Models.Enums;

// Mức độ khẩn của phiếu bảo trì. Giá trị số càng lớn càng gấp — danh sách phiếu sắp theo thứ tự này (giảm dần).
public enum TicketPriority
{
    Low = 0,      // Thấp
    Normal = 1,   // Bình thường (mặc định)
    High = 2,     // Cao
    Urgent = 3    // Khẩn cấp
}
