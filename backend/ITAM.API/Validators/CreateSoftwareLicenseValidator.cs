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
            .Must(date => date.Year >= 1990 && date.Year <= 2100)
            .WithMessage("Ngày hết hạn không hợp lệ (phải trong khoảng năm 1990–2100).");

        RuleFor(dto => dto.MaxUsage)
            .InclusiveBetween(1, 100000).WithMessage("MaxUsage phải là số nguyên từ 1 đến 100.000.");

        RuleFor(dto => dto.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
