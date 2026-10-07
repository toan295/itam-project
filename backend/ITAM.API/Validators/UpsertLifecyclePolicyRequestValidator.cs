using FluentValidation;
using ITAM.API.Models.DTOs.Lifecycle;

namespace ITAM.API.Validators;

public class UpsertLifecyclePolicyRequestValidator : AbstractValidator<UpsertLifecyclePolicyRequestDto>
{
    public UpsertLifecyclePolicyRequestValidator()
    {
        RuleFor(dto => dto.MaxAgeYears)
            .InclusiveBetween(1, 30)
            .WithMessage("Tuổi tối đa phải từ 1 đến 30 năm.");

        RuleFor(dto => dto.MaxFailureCount)
            .InclusiveBetween(1, 100)
            .WithMessage("Số lần lỗi tối đa phải từ 1 đến 100.");
    }
}
