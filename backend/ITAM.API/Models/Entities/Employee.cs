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
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Department Department { get; set; } = null!;
}
