using FluentValidation;
using ITAM.API.Models.DTOs.AssetAllocations;

namespace ITAM.API.Validators;

public class ReturnAssetAllocationValidator : AbstractValidator<ReturnAssetAllocationDto>
{
    public ReturnAssetAllocationValidator()
    {
        RuleFor(x => x.ReturnedDate)
            .NotEmpty().WithMessage("Ngày thu hồi không được để trống.");

        RuleFor(x => x.Condition)
            .NotEmpty().WithMessage("Tình trạng tài sản khi nhận lại không được để trống.")
            .Must(condition => condition is "Good" or "Damaged")
            .WithMessage("Tình trạng tài sản phải là Good hoặc Damaged.");

        RuleFor(x => x.ReturnNote)
            .MaximumLength(500).WithMessage("Ghi chú thu hồi không được vượt quá 500 ký tự.");
    }
}
