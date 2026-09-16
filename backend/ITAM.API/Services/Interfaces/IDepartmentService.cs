using ITAM.API.Models.DTOs.Departments;

namespace ITAM.API.Services.Interfaces;

public interface IDepartmentService
{
    Task<List<DepartmentResponseDto>> GetAllAsync();
    Task<DepartmentResponseDto> GetByIdAsync(int id);
    Task<DepartmentResponseDto> CreateAsync(CreateDepartmentRequestDto dto);
}
