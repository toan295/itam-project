namespace ITAM.API.Models.DTOs.Departments;

public class CreateDepartmentRequestDto
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}
