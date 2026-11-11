using ITAM.API.Helpers;
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

        // Số truy vấn cố định rồi ghép trong bộ nhớ — không N+1.
        var plans = (await _repo.GetPlansAsync(year, departmentId)).ToDictionary(p => p.DepartmentId);
        var baseline = await _repo.GetActiveEmployeeCountsAsync();
        var editors = await _repo.GetUserNamesAsync(plans.Values.Where(p => p.UpdatedByUserId.HasValue).Select(p => p.UpdatedByUserId!.Value));

        return departments.Select(d =>
        {
            plans.TryGetValue(d.Id, out var plan);
            return ToDto(year, d.Id, d.Name, plan, baseline.GetValueOrDefault(d.Id), editors);
        }).ToList();
    }

    public async Task<ExpansionPlanDto> UpsertPlanAsync(int year, int departmentId, UpsertExpansionPlanRequestDto dto, int? actorUserId = null)
    {
        EnsureValidYear(year);
        // Service tự kiểm tra lại vì có thể được gọi từ nơi khác ngoài Controller.
        if (dto.AdditionalHeadcount < UpsertExpansionPlanRequestValidator.MinHeadcount
            || dto.AdditionalHeadcount > UpsertExpansionPlanRequestValidator.MaxHeadcount)
            throw new ArgumentException(
                $"Thay đổi nhân sự phải từ {UpsertExpansionPlanRequestValidator.MinHeadcount} đến {UpsertExpansionPlanRequestValidator.MaxHeadcount} (âm = giảm).");
        if (dto.Notes is { Length: > UpsertExpansionPlanRequestValidator.NotesMaxLength })
            throw new ArgumentException($"Ghi chú tối đa {UpsertExpansionPlanRequestValidator.NotesMaxLength} ký tự.");

        var department = await _repo.GetDepartmentAsync(departmentId)
            ?? throw new DepartmentNotFoundException(departmentId);

        var plan = await _repo.GetPlanAsync(year, departmentId);
        ForecastInputGuard.EnsureVersion(plan is not null, plan?.Version, dto.Version, $"Kế hoạch nhân sự năm {year} của '{department.Name}'");

        var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        if (plan is null)
        {
            plan = new DepartmentExpansionPlan { Year = year, DepartmentId = departmentId };
            await _repo.AddPlanAsync(plan);
        }
        else if (plan.AdditionalHeadcount == dto.AdditionalHeadcount && plan.Notes == notes)
        {
            // Không đổi gì: không lưu (không tăng phiên bản, không đổi "cập nhật gần nhất").
            return await BuildDtoAsync(plan, department);
        }

        plan.AdditionalHeadcount = dto.AdditionalHeadcount;
        plan.Notes = notes;
        plan.UpdatedAt = ForecastInputGuard.NowUtc();
        plan.UpdatedByUserId = actorUserId;

        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ForecastInputConflictException(
                $"Kế hoạch nhân sự năm {year} của '{department.Name}' vừa được người khác thay đổi trong lúc bạn lưu. Vui lòng tải lại trang.");
        }
        catch (DbUpdateException)
        {
            // Race hiếm: 2 request cùng tạo kế hoạch của (năm, phòng ban) và cùng vượt qua bước kiểm tra ở trên.
            // Unique index (Year, DepartmentId) là chốt chặn cuối — ánh xạ thành 409 thay vì 500 (mẫu AssetService).
            throw new ExpansionPlanConflictException(year, departmentId);
        }

        return await BuildDtoAsync(plan, department);
    }

    public async Task DeletePlanAsync(int year, int departmentId, int? version = null)
    {
        ForecastInputGuard.RequireVersionForDelete(version);
        var plan = await _repo.GetPlanAsync(year, departmentId)
            ?? throw new ExpansionPlanNotFoundException(year, departmentId);
        ForecastInputGuard.EnsureVersion(true, plan.Version, version, $"Kế hoạch nhân sự năm {year}");

        _repo.RemovePlan(plan);
        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ForecastInputConflictException("Kế hoạch nhân sự vừa được người khác thay đổi hoặc đã bị xoá. Vui lòng tải lại trang.");
        }
    }

    public async Task<List<DeviceRatioDto>> GetRatiosAsync()
    {
        var categories = await _repo.GetAllCategoriesAsync();
        var ratios = (await _repo.GetAllRatiosAsync()).ToDictionary(r => r.CategoryId);
        var editors = await _repo.GetUserNamesAsync(ratios.Values.Where(r => r.UpdatedByUserId.HasValue).Select(r => r.UpdatedByUserId!.Value));

        return categories.Select(c =>
        {
            ratios.TryGetValue(c.Id, out var ratio);
            return ToDto(c.Id, c.Name, ratio, editors);
        }).ToList();
    }

    public async Task<DeviceRatioDto> UpsertRatioAsync(int categoryId, UpsertDeviceRatioRequestDto dto, int? actorUserId = null)
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
        ForecastInputGuard.EnsureVersion(ratio is not null, ratio?.Version, dto.Version, $"Định mức của loại '{category.Name}'");

        if (ratio is null)
        {
            ratio = new AssetCategoryDeviceRatio { CategoryId = categoryId };
            await _repo.AddRatioAsync(ratio);
        }
        else if (ratio.DevicesPerPerson == dto.DevicesPerPerson)
        {
            return ToDto(categoryId, category.Name, ratio, await EditorNamesAsync(ratio.UpdatedByUserId));
        }

        ratio.DevicesPerPerson = dto.DevicesPerPerson;
        ratio.UpdatedAt = ForecastInputGuard.NowUtc();
        ratio.UpdatedByUserId = actorUserId;

        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ForecastInputConflictException($"Định mức của loại '{category.Name}' vừa được người khác thay đổi trong lúc bạn lưu. Vui lòng tải lại trang.");
        }
        catch (DbUpdateException)
        {
            // Hai yêu cầu cùng tạo định mức cho một loại (khoá chính CategoryId) — bên chậm hơn nhận 409 thay vì 500.
            throw new ForecastInputConflictException($"Loại '{category.Name}' vừa được người khác tạo định mức. Vui lòng tải lại trang.");
        }

        return ToDto(categoryId, category.Name, ratio, await EditorNamesAsync(actorUserId));
    }

    public async Task DeleteRatioAsync(int categoryId, int? version = null)
    {
        ForecastInputGuard.RequireVersionForDelete(version);
        var ratio = await _repo.GetRatioAsync(categoryId)
            ?? throw new DeviceRatioNotFoundException(categoryId);
        ForecastInputGuard.EnsureVersion(true, ratio.Version, version, "Định mức");

        _repo.RemoveRatio(ratio);
        try
        {
            await _repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ForecastInputConflictException("Định mức vừa được người khác thay đổi hoặc đã bị xoá. Vui lòng tải lại trang.");
        }
    }

    private async Task<Dictionary<int, string>> EditorNamesAsync(int? userId) =>
        userId.HasValue ? await _repo.GetUserNamesAsync(new[] { userId.Value }) : new Dictionary<int, string>();

    private async Task<ExpansionPlanDto> BuildDtoAsync(DepartmentExpansionPlan plan, (int Id, string Name) department)
    {
        var baseline = await _repo.GetActiveEmployeeCountsAsync();
        return ToDto(plan.Year, department.Id, department.Name, plan, baseline.GetValueOrDefault(department.Id), await EditorNamesAsync(plan.UpdatedByUserId));
    }

    // Năm theo lịch Việt Nam (cùng "hôm nay" với module Vòng đời).
    private static void EnsureValidYear(int year)
    {
        var currentYear = VietnamTime.Today().Year;
        if (year < currentYear || year > currentYear + MaxForecastYearsAhead)
            throw new ArgumentException($"Năm phải từ {currentYear} đến {currentYear + MaxForecastYearsAhead}.");
    }

    private static string? EditorName(int? userId, IReadOnlyDictionary<int, string> editors) =>
        userId is { } id ? (editors.TryGetValue(id, out var name) ? name : "(tài khoản không còn)") : null;

    private static ExpansionPlanDto ToDto(
        int year, int departmentId, string departmentName, DepartmentExpansionPlan? plan, int baseline, IReadOnlyDictionary<int, string> editors) => new()
    {
        Year = year,
        DepartmentId = departmentId,
        DepartmentName = departmentName,
        AdditionalHeadcount = plan?.AdditionalHeadcount ?? 0,
        Notes = plan?.Notes,
        UpdatedAt = ForecastInputGuard.AsUtc(plan?.UpdatedAt),
        HasPlan = plan is not null,
        Version = plan?.Version,
        UpdatedByName = EditorName(plan?.UpdatedByUserId, editors),
        BaselineHeadcount = baseline,
        ProjectedHeadcount = Math.Max(0, baseline + (plan?.AdditionalHeadcount ?? 0)),
    };

    private static DeviceRatioDto ToDto(int categoryId, string categoryName, AssetCategoryDeviceRatio? ratio, IReadOnlyDictionary<int, string> editors) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        DevicesPerPerson = ratio?.DevicesPerPerson,
        Version = ratio?.Version,
        UpdatedAt = ForecastInputGuard.AsUtc(ratio?.UpdatedAt),
        UpdatedByName = EditorName(ratio?.UpdatedByUserId, editors),
    };
}
