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
        var name = dto.Name.Trim();

        // Không có check này thì Admin IT tạo được nhiều phòng ban trùng tên (Department.Name hiện
        // chưa có unique index ở DB như Role.Name — xem AppDbContext.cs), gây nhầm lẫn khi Manager
        // được gán vào "đúng" phòng ban nhưng có 2 bản ghi cùng tên. Do chưa có ràng buộc DB, vẫn còn
        // race condition hiếm (2 request tạo cùng lúc) — nên bổ sung unique index thật sự ở DB sau,
        // cần trao đổi với Hoàng Đức Tú vì AppDbContext.cs là file dùng chung.
        var existed = await _repo.GetByNameAsync(name);
        if (existed is not null)
        {
            throw new DepartmentNameAlreadyExistsException(name);
        }

        var department = new Department
        {
            Name = name,
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
