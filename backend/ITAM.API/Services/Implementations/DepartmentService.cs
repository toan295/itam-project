using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;

namespace ITAM.API.Services.Implementations;

public class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repo;

    public DepartmentService(IDepartmentRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<DepartmentResponseDto>> GetAllAsync()
    {
        var departments = await _repo.GetAllAsync();
        return departments.Select(MapToDto).ToList();
    }

    public async Task<DepartmentResponseDto> GetByIdAsync(int id)
    {
        var department = await _repo.GetByIdAsync(id)
            ?? throw new DepartmentNotFoundException(id);
        return MapToDto(department);
    }

    public async Task<DepartmentResponseDto> CreateAsync(CreateDepartmentRequestDto dto)
    {
        var department = new Department
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
        };

        await _repo.AddAsync(department);
        await _repo.SaveChangesAsync();

        return MapToDto(department);
    }

    private static DepartmentResponseDto MapToDto(Department d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
    };
}
