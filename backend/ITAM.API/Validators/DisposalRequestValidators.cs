using FluentValidation;
using ITAM.API.Models.DTOs.Disposals;

namespace ITAM.API.Validators;

public class CreateDisposalRequestValidator : AbstractValidator<CreateDisposalRequestDto>
{
    public CreateDisposalRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Phải chọn tài sản.");
        RuleFor(x => x.InspectionNote).NotEmpty().WithMessage("Kết quả kiểm tra không được để trống.")
            .MaximumLength(2000).WithMessage("Kết quả kiểm tra không được vượt quá 2000 ký tự.");
    }
}

public class ProposeDisposalRequestValidator : AbstractValidator<ProposeDisposalRequestDto>
{
    public ProposeDisposalRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Lý do đề xuất không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do đề xuất không được vượt quá 1000 ký tự.");
        RuleFor(x => x.DisposalMethod).MaximumLength(100).WithMessage("Hình thức thanh lý không được vượt quá 100 ký tự.");
    }
}

public class ReviewDisposalRequestValidator : AbstractValidator<ReviewDisposalRequestDto>
{
    public ReviewDisposalRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage("Ghi chú không được vượt quá 1000 ký tự.");
    }
}

public class CompleteDisposalRequestValidator : AbstractValidator<CompleteDisposalRequestDto>
{
    public CompleteDisposalRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage("Ghi chú không được vượt quá 1000 ký tự.");
    }
}
