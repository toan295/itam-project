using FluentValidation;
using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Validators;

public class UpsertExpansionPlanRequestValidator : AbstractValidator<UpsertExpansionPlanRequestDto>
{
    public const int MaxHeadcount = 5000;
    public const int NotesMaxLength = 500;

    public UpsertExpansionPlanRequestValidator()
    {
        RuleFor(x => x.AdditionalHeadcount)
            .InclusiveBetween(0, MaxHeadcount)
            .WithMessage($"Số nhân sự tăng thêm phải từ 0 đến {MaxHeadcount}.");

        RuleFor(x => x.Notes)
            .MaximumLength(NotesMaxLength)
            .WithMessage($"Ghi chú tối đa {NotesMaxLength} ký tự.");
    }
}
