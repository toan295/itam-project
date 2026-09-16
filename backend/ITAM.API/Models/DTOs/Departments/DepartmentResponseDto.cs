namespace ITAM.API.Models.DTOs.Departments;

public class DepartmentResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
