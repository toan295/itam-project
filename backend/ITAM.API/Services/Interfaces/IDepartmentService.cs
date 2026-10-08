using ITAM.API.Models.DTOs.Departments;

namespace ITAM.API.Services.Interfaces;

public interface IDepartmentService
{
    Task<List<DepartmentResponseDto>> GetAllAsync();
    Task<DepartmentResponseDto> GetByIdAsync(int id);
    Task<DepartmentResponseDto> CreateAsync(CreateDepartmentRequestDto dto);
    Task<DepartmentResponseDto> UpdateAsync(int id, UpdateDepartmentRequestDto dto);
    Task DeleteAsync(int id);
}
