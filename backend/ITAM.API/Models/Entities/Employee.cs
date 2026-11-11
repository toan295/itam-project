namespace ITAM.API.Models.Entities;

// Danh mục nhân viên nhận tài sản. Người nhận khi phân bổ phải CHỌN từ danh mục này (không gõ tay) để dữ
// liệu thống nhất, tìm kiếm được và in đủ thông tin (họ tên, chức danh, bộ phận) lên biên bản bàn giao.
// Không phải tài khoản đăng nhập — nhân viên không có tài khoản vẫn nhận được tài sản.
public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = null!;   // NV0001... sinh tự động.
    public string FullName { get; set; } = null!;
    public int DepartmentId { get; set; }
    public string? Position { get; set; }                // Chức danh.
    // Email liên hệ của nhân viên; cũng là email đăng nhập/nhận thư kích hoạt khi tạo tài khoản. Duy nhất. Null = nhân viên cũ chưa bổ sung.
    public string? Email { get; set; }
    public string? Phone { get; set; }                   // Số điện thoại (đã chuẩn hoá, xem PhoneNumber).
    // Tài khoản đăng nhập của nhân viên (vai trò Employee) để tự xem tài sản được cấp và gửi yêu cầu bảo trì. Null = chưa cấp tài khoản.
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Xoá mềm: bấm "Xoá" trên giao diện chỉ đánh dấu dòng này, dữ liệu vẫn còn trong CSDL (và không hiện lại khi chạy lại hệ thống).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Department Department { get; set; } = null!;
    public User? User { get; set; }
}
