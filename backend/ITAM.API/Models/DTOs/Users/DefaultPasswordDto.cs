namespace ITAM.API.Models.DTOs.Users;

public class DefaultPasswordDto
{
    public bool IsConfigured { get; set; }

    // null khi chưa cấu hình. Chỉ Admin IT gọi được endpoint trả DTO này.
    public string? Password { get; set; }
}

public class SetDefaultPasswordRequestDto
{
    public string Password { get; set; } = string.Empty;
}
