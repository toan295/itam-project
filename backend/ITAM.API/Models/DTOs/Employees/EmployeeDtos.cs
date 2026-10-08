namespace ITAM.API.Models.DTOs.Employees;

public class EmployeeResponseDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public string? Position { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertEmployeeRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
}
