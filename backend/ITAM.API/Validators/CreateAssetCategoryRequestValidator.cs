using FluentValidation;
using ITAM.API.Models.DTOs.AssetCategories;

namespace ITAM.API.Validators;

public class CreateAssetCategoryRequestValidator : AbstractValidator<CreateAssetCategoryRequestDto>
{
    public CreateAssetCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
