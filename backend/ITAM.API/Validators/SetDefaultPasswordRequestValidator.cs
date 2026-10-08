using FluentValidation;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Users;

namespace ITAM.API.Validators;

public class SetDefaultPasswordRequestValidator : AbstractValidator<SetDefaultPasswordRequestDto>
{
    public SetDefaultPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu mặc định không được để trống.")
            .Must(p => PasswordPolicy.Validate(p) is null)
            .WithMessage(x => PasswordPolicy.Validate(x.Password) ?? string.Empty)
            .When(x => !string.IsNullOrEmpty(x.Password))
            .Must(p => p == p.Trim()).WithMessage("Mật khẩu mặc định không được bắt đầu hoặc kết thúc bằng khoảng trắng.");
    }
}
