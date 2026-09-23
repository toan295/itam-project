using FluentValidation;
using ITAM.API.Models.DTOs.AssetAllocations;

namespace ITAM.API.Validators;

public class CreateAssetAllocationValidator : AbstractValidator<CreateAssetAllocationDto>
{
    public CreateAssetAllocationValidator()
    {
        RuleFor(x => x.AssetId)
            .GreaterThan(0).WithMessage("AssetId phải lớn hơn 0.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).WithMessage("DepartmentId phải lớn hơn 0.");

        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Tên người nhận không được để trống.")
            .MaximumLength(100).WithMessage("Tên người nhận không được vượt quá 100 ký tự.");

        RuleFor(x => x.AllocatedDate)
            .NotEmpty().WithMessage("Ngày phân bổ không được để trống.");

        RuleFor(x => x.HandoverNote)
            .MaximumLength(500).WithMessage("Ghi chú bàn giao không được vượt quá 500 ký tự.");
    }
}
