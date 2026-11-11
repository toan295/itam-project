using FluentValidation;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Phải chọn vai trò (Role).");
        RuleFor(x => x.EmployeeId).GreaterThan(0)
            .WithMessage("Phải chọn nhân viên: tài khoản chỉ tạo cho nhân viên đã có trong Danh mục nhân viên.");
    }
}
