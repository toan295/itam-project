using FluentValidation;
using ITAM.API.Models.DTOs.SoftwareLicenses;

namespace ITAM.API.Validators;

public class CreateSoftwareLicenseValidator : AbstractValidator<CreateSoftwareLicenseDto>
{
    public CreateSoftwareLicenseValidator()
    {
        RuleFor(dto => dto.SoftwareName)
            .NotEmpty().WithMessage("Tên phần mềm không được để trống.")
            .MaximumLength(150).WithMessage("Tên phần mềm không được vượt quá 150 ký tự.");

        RuleFor(dto => dto.LicenseKey)
            .NotEmpty().WithMessage("LicenseKey không được để trống.")
            .MaximumLength(255).WithMessage("LicenseKey không được vượt quá 255 ký tự.");

        RuleFor(dto => dto.ExpiryDate)
            .Must(date => date != default)
            .WithMessage("Ngày hết hạn không hợp lệ.");

        RuleFor(dto => dto.MaxUsage)
            .GreaterThan(0).WithMessage("MaxUsage phải lớn hơn 0.");

        RuleFor(dto => dto.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
