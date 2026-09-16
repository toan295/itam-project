using FluentValidation;
using ITAM.API.Models.DTOs.Assets;

namespace ITAM.API.Validators;

public class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequestDto>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.AssetCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Phải chọn loại tài sản.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phải chọn phòng ban.");
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.OperatingSystem).MaximumLength(100);
        RuleFor(x => x.WarrantyExpiry)
            .GreaterThanOrEqualTo(x => x.PurchaseDate!.Value)
            .When(x => x.PurchaseDate.HasValue && x.WarrantyExpiry.HasValue)
            .WithMessage("Hạn bảo hành phải từ ngày mua trở về sau.");
    }
}
