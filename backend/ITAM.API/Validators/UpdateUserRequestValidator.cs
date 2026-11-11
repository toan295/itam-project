using FluentValidation;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Phải chọn vai trò (Role).");
        RuleFor(x => x.FullName).MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.")
            .Must(v => v is null || !string.IsNullOrWhiteSpace(v)).WithMessage("Họ tên không được để trống.");
        RuleFor(x => x.Email).MaximumLength(150).WithMessage("Email không được vượt quá 150 ký tự.")
            .EmailAddress().WithMessage("Email không hợp lệ.").When(x => x.Email is not null);
        RuleFor(x => x.Phone).Must(v => string.IsNullOrWhiteSpace(v) || ITAM.API.Helpers.PhoneNumber.IsValid(v))
            .WithMessage(ITAM.API.Helpers.PhoneNumber.InvalidMessage);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phòng ban không hợp lệ.").When(x => x.DepartmentId.HasValue);
    }
}
