using FluentValidation;
using ITAM.API.Models.DTOs.Departments;

namespace ITAM.API.Validators;

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequestDto>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên phòng ban không được để trống.")
            .MaximumLength(100).WithMessage("Tên phòng ban không được vượt quá 100 ký tự.");
        RuleFor(x => x.Description).MaximumLength(255).WithMessage("Mô tả không được vượt quá 255 ký tự.");
    }
}
