using FluentValidation;
using ITAM.API.Models.DTOs.Departments;

namespace ITAM.API.Validators;

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequestDto>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(255);
    }
}
