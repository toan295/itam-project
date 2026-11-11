using FluentValidation;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Validators;

public class GenerateForecastRequestValidator : AbstractValidator<GenerateForecastRequestDto>
{
    public GenerateForecastRequestValidator()
    {
        // D6: từ năm hiện tại đến +10. Tính lúc validate (không cache) để không lệch khi sang năm mới.
        RuleFor(x => x.Year)
            .Must(y => y >= VietnamTime.Today().Year && y <= VietnamTime.Today().Year + 10)
            .WithMessage(_ => $"Năm dự báo phải từ {VietnamTime.Today().Year} đến {VietnamTime.Today().Year + 10}.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).When(x => x.DepartmentId.HasValue)
            .WithMessage("Phòng ban không hợp lệ.");
    }
}
