using FluentValidation;
using ITAM.API.Models.DTOs.SoftwareLicenses;

namespace ITAM.API.Validators;

public class AssignSoftwareLicenseValidator : AbstractValidator<AssignSoftwareLicenseDto>
{
    public AssignSoftwareLicenseValidator()
    {
        RuleFor(dto => dto.AssetId)
            .GreaterThan(0).WithMessage("AssetId phải lớn hơn 0.");
    }
}
