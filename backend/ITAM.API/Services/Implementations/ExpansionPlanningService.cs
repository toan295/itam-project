using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.Entities;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.EntityFrameworkCore;

namespace ITAM.API.Services.Implementations;

public class ExpansionPlanningService : IExpansionPlanningService
{
    private const int MaxForecastYearsAhead = 10;   // cùng khoảng năm với GenerateAsync (D6)

    private readonly IBudgetForecastRepository _repo;

    public ExpansionPlanningService(IBudgetForecastRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<ExpansionPlanDto>> GetPlansAsync(int year, int? departmentId)
    {
        EnsureValidYear(year);

        List<(int Id, string Name)> departments;
        if (departmentId.HasValue)
        {
            var department = await _repo.GetDepartmentAsync(departmentId.Value)
                ?? throw new DepartmentNotFoundException(departmentId.Value);
            departments = new List<(int Id, string Name)> { department };
        }
        else
        {
            departments = await _repo.GetAllDepartmentsAsync();
        }

        // 2 truy vấn cố định rồi ghép trong bộ nhớ — không N+1.
        var plans = (await _repo.GetPlansAsync(year, departmentId)).ToDictionary(p => p.DepartmentId);

        return departments.Select(d => plans.TryGetValue(d.Id, out var plan)
            ? ToDto(plan, d.Name)
            : new ExpansionPlanDto { Year = year, DepartmentId = d.Id, DepartmentName = d.Name }).ToList();
    }

    public async Task<ExpansionPlanDto> UpsertPlanAsync(int year, int departmentId, UpsertExpansionPlanRequestDto dto)
    {
        EnsureValidYear(year);
        // Service tự kiểm tra lại vì có thể được gọi từ nơi khác ngoài Controller.
        if (dto.AdditionalHeadcount < 0 || dto.AdditionalHeadcount > UpsertExpansionPlanRequestValidator.MaxHeadcount)
            throw new ArgumentException($"Số nhân sự tăng thêm phải từ 0 đến {UpsertExpansionPlanRequestValidator.MaxHeadcount}.");
        if (dto.Notes is { Length: > UpsertExpansionPlanRequestValidator.NotesMaxLength })
            throw new ArgumentException($"Ghi chú tối đa {UpsertExpansionPlanRequestValidator.NotesMaxLength} ký tự.");

        var department = await _repo.GetDepartmentAsync(departmentId)
            ?? throw new DepartmentNotFoundException(departmentId);

        var plan = await _repo.GetPlanAsync(year, departmentId);
        if (plan is null)
        {
            plan = new DepartmentExpansionPlan { Year = year, DepartmentId = departmentId };
            await _repo.AddPlanAsync(plan);
        }

        plan.AdditionalHeadcount = dto.AdditionalHeadcount;
        plan.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        plan.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race hiếm: 2 request cùng tạo kế hoạch của (năm, phòng ban) và cùng vượt qua bước kiểm tra ở trên.
            // Unique index (Year, DepartmentId) là chốt chặn cuối — ánh xạ thành 409 thay vì 500 (mẫu AssetService).
            throw new ExpansionPlanConflictException(year, departmentId);
        }

        return ToDto(plan, department.Name);
    }

    public async Task DeletePlanAsync(int year, int departmentId)
    {
        var plan = await _repo.GetPlanAsync(year, departmentId)
            ?? throw new ExpansionPlanNotFoundException(year, departmentId);

        _repo.RemovePlan(plan);
        await _repo.SaveChangesAsync();
    }

    public async Task<List<DeviceRatioDto>> GetRatiosAsync()
    {
        var categories = await _repo.GetAllCategoriesAsync();
        var ratios = (await _repo.GetAllRatiosAsync()).ToDictionary(r => r.CategoryId, r => r.DevicesPerPerson);

        return categories.Select(c => new DeviceRatioDto
        {
            CategoryId = c.Id,
            CategoryName = c.Name,
            DevicesPerPerson = ratios.TryGetValue(c.Id, out var ratio) ? ratio : null,
        }).ToList();
    }

    public async Task<DeviceRatioDto> UpsertRatioAsync(int categoryId, UpsertDeviceRatioRequestDto dto)
    {
        if (dto.DevicesPerPerson < UpsertDeviceRatioRequestValidator.MinRatio
            || dto.DevicesPerPerson > UpsertDeviceRatioRequestValidator.MaxRatio
            || decimal.Round(dto.DevicesPerPerson, 2) != dto.DevicesPerPerson)
            throw new ArgumentException("Định mức phải từ 0,01 đến 20 thiết bị/người, tối đa 2 chữ số thập phân.");

        var categories = await _repo.GetAllCategoriesAsync();
        var category = categories.FirstOrDefault(c => c.Id == categoryId);
        if (category == default)
            throw new AssetCategoryNotFoundException(categoryId);

        var ratio = await _repo.GetRatioAsync(categoryId);
        if (ratio is null)
        {
            ratio = new AssetCategoryDeviceRatio { CategoryId = categoryId, DevicesPerPerson = dto.DevicesPerPerson };
            await _repo.AddRatioAsync(ratio);
        }
        else
        {
            ratio.DevicesPerPerson = dto.DevicesPerPerson;
        }

        await _repo.SaveChangesAsync();

        return new DeviceRatioDto { CategoryId = categoryId, CategoryName = category.Name, DevicesPerPerson = ratio.DevicesPerPerson };
    }

    public async Task DeleteRatioAsync(int categoryId)
    {
        var ratio = await _repo.GetRatioAsync(categoryId)
            ?? throw new DeviceRatioNotFoundException(categoryId);

        _repo.RemoveRatio(ratio);
        await _repo.SaveChangesAsync();
    }

    private static void EnsureValidYear(int year)
    {
        var currentYear = DateTime.UtcNow.Year;
        if (year < currentYear || year > currentYear + MaxForecastYearsAhead)
            throw new ArgumentException($"Năm phải từ {currentYear} đến {currentYear + MaxForecastYearsAhead}.");
    }

    private static ExpansionPlanDto ToDto(DepartmentExpansionPlan plan, string departmentName) => new()
    {
        Year = plan.Year,
        DepartmentId = plan.DepartmentId,
        DepartmentName = departmentName,
        AdditionalHeadcount = plan.AdditionalHeadcount,
        Notes = plan.Notes,
        UpdatedAt = plan.UpdatedAt,
        HasPlan = true,
    };
}
