using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        // Kiểm tra trước để trả 409 thân thiện; unique index trên Department.Name (migration
        // AddDepartmentUniqueIndexAndRestrictLicenseDelete) là chốt chặn cuối cho race hiếm.
        var existed = await _repo.GetByNameAsync(name);
        if (existed is not null)
        {
            throw new DepartmentNameAlreadyExistsException(name);
        }

        var department = new Department
        {
            Name = name,
            Description = NormalizeOptionalText(dto.Description),
        };

        await _repo.AddAsync(department);
        await SaveChangesGuardingNameConflictAsync(name);

        return MapToDto(department);
    }

    public async Task<DepartmentResponseDto> UpdateAsync(int id, UpdateDepartmentRequestDto dto)
    {
        var department = await _repo.GetByIdAsync(id)
            ?? throw new DepartmentNotFoundException(id);

        var name = dto.Name.Trim();
        if (!string.Equals(name, department.Name, StringComparison.Ordinal))
        {
            var existed = await _repo.GetByNameAsync(name);
            if (existed is not null && existed.Id != id)
            {
                throw new DepartmentNameAlreadyExistsException(name);
            }
        }

        department.Name = name;
        department.Description = NormalizeOptionalText(dto.Description);

        _repo.Update(department);
        await SaveChangesGuardingNameConflictAsync(name);

        return MapToDto(department);
    }

    public async Task DeleteAsync(int id)
    {
        var department = await _repo.GetByIdAsync(id)
            ?? throw new DepartmentNotFoundException(id);

        // UC-04 E1 + quy tắc "không xoá cứng danh mục đang sử dụng": chặn khi còn người dùng, tài sản,
        // bản ghi phân bổ hoặc dự báo ngân sách thuộc phòng ban này.
        if (await _repo.IsReferencedAsync(id))
        {
            throw new DepartmentInUseException(id);
        }

        _repo.Remove(department);

        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race hiếm: có bản ghi mới tham chiếu phòng ban này ngay giữa lúc kiểm tra và xoá
            // (FK Restrict ở DB từ chối) — vẫn trả 409 thay vì 500.
            throw new DepartmentInUseException(id);
        }
    }

    private async Task SaveChangesGuardingNameConflictAsync(string name)
    {
        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new DepartmentNameAlreadyExistsException(name);
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DepartmentResponseDto MapToDto(Department d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
    };
}
