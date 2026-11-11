namespace ITAM.API.Models.DTOs.Employees;

public class EmployeeResponseDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }

    // Đã cấp tài khoản đăng nhập (vai trò Employee) chưa; AccountUserId là Id trong bảng Users.
    public bool HasAccount { get; set; }
    public int? AccountUserId { get; set; }

    // Vai trò của tài khoản gắn với nhân viên (Admin IT / Manager / Technician / Employee); null = chưa có tài khoản.
    public string? RoleName { get; set; }

    // Tài khoản đang chờ người dùng kích hoạt / đặt mật khẩu.
    public bool PendingActivation { get; set; }

    // Tài khoản gắn với nhân viên có đang dùng được không (false = đã bị khoá); null = chưa có tài khoản.
    public bool? AccountIsActive { get; set; }
}

public class UpsertEmployeeRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? Position { get; set; }
    // Bắt buộc: email này dùng để gửi liên kết kích hoạt khi cấp tài khoản đăng nhập cho nhân viên.
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
