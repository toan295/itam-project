using FluentValidation;
using ITAM.API.Models.DTOs.Forecasts;

namespace ITAM.API.Validators;

public class UpsertExpansionPlanRequestValidator : AbstractValidator<UpsertExpansionPlanRequestDto>
{
    public const int MaxHeadcount = 5000;
    public const int MinHeadcount = -5000;
    public const int NotesMaxLength = 500;

    public UpsertExpansionPlanRequestValidator()
    {
        RuleFor(x => x.AdditionalHeadcount)
            .InclusiveBetween(MinHeadcount, MaxHeadcount)
            .WithMessage($"Thay đổi nhân sự phải từ {MinHeadcount} đến {MaxHeadcount} (âm = giảm).");

        RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version.HasValue).WithMessage("Version không hợp lệ.");

        RuleFor(x => x.Notes)
            .MaximumLength(NotesMaxLength)
            .WithMessage($"Ghi chú tối đa {NotesMaxLength} ký tự.");
    }
}
