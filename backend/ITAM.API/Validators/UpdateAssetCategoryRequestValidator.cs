using FluentValidation;
using ITAM.API.Models.DTOs.AssetCategories;

namespace ITAM.API.Validators;

public class UpdateAssetCategoryRequestValidator : AbstractValidator<UpdateAssetCategoryRequestDto>
{
    public UpdateAssetCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
