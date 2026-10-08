using FluentValidation;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.Enums;

namespace ITAM.API.Validators;

public class UpsertDisposalStatusRequestValidator : AbstractValidator<UpsertDisposalStatusRequestDto>
{
    public UpsertDisposalStatusRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên trạng thái không được để trống.")
            .MaximumLength(100).WithMessage("Tên trạng thái không được vượt quá 100 ký tự.");
        RuleFor(x => x.Description).MaximumLength(255).WithMessage("Mô tả không được vượt quá 255 ký tự.");
        RuleFor(x => x.Color).Must(c => DisposalStatusCodes.AllowedColors.Contains(c))
            .WithMessage($"Màu không hợp lệ. Phải là một trong: {string.Join(", ", DisposalStatusCodes.AllowedColors)}.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000).WithMessage("Thứ tự phải nằm trong khoảng 0–1000.");
    }
}
