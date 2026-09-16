using FluentValidation;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.Enums;

namespace ITAM.API.Validators;

public class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequestDto>
{
    public UpdateAssetRequestValidator()
    {
        RuleFor(x => x.AssetCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Phải chọn loại tài sản.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Phải chọn phòng ban.");
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Specification).MaximumLength(500);
        RuleFor(x => x.OperatingSystem).MaximumLength(100);
        // Enum.TryParse mặc định chấp nhận cả chuỗi số ("3" -> Disposed) — dùng Enum.GetNames để
        // chỉ chấp nhận đúng tên trạng thái, đúng ý định thiết kế (xem UpdateAssetRequestDto.Status).
        RuleFor(x => x.Status)
            .Must(s => Enum.GetNames<AssetStatus>().Contains(s))
            .WithMessage($"Status phải là một trong: {string.Join(", ", Enum.GetNames<AssetStatus>())}");
        RuleFor(x => x.WarrantyExpiry)
            .GreaterThanOrEqualTo(x => x.PurchaseDate!.Value)
            .When(x => x.PurchaseDate.HasValue && x.WarrantyExpiry.HasValue)
            .WithMessage("Hạn bảo hành phải từ ngày mua trở về sau.");
    }
}
