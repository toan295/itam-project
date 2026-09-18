using FluentValidation;
using ITAM.API.Models.DTOs.AssetCategories;

namespace ITAM.API.Validators;

public class UpdateAssetCategoryRequestValidator : AbstractValidator<UpdateAssetCategoryRequestDto>
{
    public UpdateAssetCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên loại tài sản không được để trống.")
            .MaximumLength(100).WithMessage("Tên loại tài sản không được vượt quá 100 ký tự.");
    }
}
