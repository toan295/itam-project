using FluentValidation;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.Enums;

namespace ITAM.API.Validators;

public class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequestDto>
{
    public UpdateAssetRequestValidator()
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
        // Enum.TryParse mặc định chấp nhận cả chuỗi số ("3" -> Disposed) — dùng Enum.GetNames để
        // chỉ chấp nhận đúng tên trạng thái, đúng ý định thiết kế (xem UpdateAssetRequestDto.Status).
        RuleFor(x => x.Status)
            .Must(s => Enum.GetNames<AssetStatus>().Contains(s))
            .WithMessage($"Trạng thái tài sản không hợp lệ. Giá trị hợp lệ: {string.Join(", ", Enum.GetNames<AssetStatus>())}.");
        RuleFor(x => x.PurchaseDate)
            .Must(d => DateRules.IsNotInFuture(d!.Value))
            .When(x => x.PurchaseDate.HasValue)
            .WithMessage("Ngày mua không được ở tương lai.");
        RuleFor(x => x.WarrantyExpiry)
            .GreaterThanOrEqualTo(x => x.PurchaseDate!.Value)
            .When(x => x.PurchaseDate.HasValue && x.WarrantyExpiry.HasValue)
            .WithMessage("Hạn bảo hành phải từ ngày mua trở về sau.");
    }
}
