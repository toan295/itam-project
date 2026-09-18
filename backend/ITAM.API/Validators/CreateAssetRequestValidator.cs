using FluentValidation;
using ITAM.API.Models.DTOs.Assets;

namespace ITAM.API.Validators;

public class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequestDto>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.AssetCode)
            .NotEmpty().WithMessage("Mã tài sản không được để trống.")
            .MaximumLength(50).WithMessage("Mã tài sản không được vượt quá 50 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tài sản không được để trống.")
            .MaximumLength(150).WithMessage("Tên tài sản không được vượt quá 150 ký tự.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Phải chọn loại tài sản.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phải chọn phòng ban.");
        RuleFor(x => x.SerialNumber).MaximumLength(100).WithMessage("Số serial không được vượt quá 100 ký tự.");
        RuleFor(x => x.Specification).MaximumLength(500).WithMessage("Thông số kỹ thuật không được vượt quá 500 ký tự.");
        RuleFor(x => x.OperatingSystem).MaximumLength(100).WithMessage("Hệ điều hành không được vượt quá 100 ký tự.");
        RuleFor(x => x.WarrantyExpiry)
            .GreaterThanOrEqualTo(x => x.PurchaseDate!.Value)
            .When(x => x.PurchaseDate.HasValue && x.WarrantyExpiry.HasValue)
            .WithMessage("Hạn bảo hành phải từ ngày mua trở về sau.");
    }
}
