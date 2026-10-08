using FluentValidation;
using ITAM.API.Models.DTOs.Employees;

namespace ITAM.API.Validators;

public class UpsertEmployeeRequestValidator : AbstractValidator<UpsertEmployeeRequestDto>
{
    public UpsertEmployeeRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phải chọn phòng ban.");
        RuleFor(x => x.Position).MaximumLength(100).WithMessage("Chức danh không được vượt quá 100 ký tự.");
    }
}
