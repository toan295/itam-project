using FluentValidation;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(150).WithMessage("Email không được vượt quá 150 ký tự.");
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Phải chọn vai trò (Role).");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phải chọn phòng ban.");
    }
}
