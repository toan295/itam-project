using FluentValidation;
using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Validators;

public class UpsertDeviceRatioRequestValidator : AbstractValidator<UpsertDeviceRatioRequestDto>
{
    public const decimal MinRatio = 0.01m;
    public const decimal MaxRatio = 20m;

    public UpsertDeviceRatioRequestValidator()
    {
        RuleFor(x => x.DevicesPerPerson)
            .InclusiveBetween(MinRatio, MaxRatio)
            .WithMessage("Định mức phải từ 0,01 đến 20 thiết bị/người.");

        RuleFor(x => x.DevicesPerPerson)
            .Must(x => decimal.Round(x, 2) == x)
            .WithMessage("Tối đa 2 chữ số thập phân.");
    }
}
