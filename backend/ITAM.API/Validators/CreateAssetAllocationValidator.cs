using FluentValidation;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs.AssetAllocations;

namespace ITAM.API.Validators;

public class CreateAssetAllocationValidator : AbstractValidator<CreateAssetAllocationDto>
{
    // Danh sách cố định để tình trạng lúc bàn giao thống nhất (không gõ tự do).
    public static readonly string[] AllowedConditions = { "Mới", "Tốt", "Bình thường", "Cũ" };

    public CreateAssetAllocationValidator()
    {
        RuleFor(x => x.AssetId)
            .GreaterThan(0).WithMessage("AssetId phải lớn hơn 0.");

        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("Phải chọn người nhận từ danh sách nhân viên.");

        RuleFor(x => x.AllocatedDate)
            .NotEmpty().WithMessage("Ngày phân bổ không được để trống.")
            .Must(DateRules.IsNotInFuture).WithMessage("Ngày phân bổ không được ở tương lai.");

        RuleFor(x => x.HandoverCondition)
            .Must(c => AllowedConditions.Contains(c))
            .WithMessage($"Tình trạng bàn giao không hợp lệ. Phải là một trong: {string.Join(", ", AllowedConditions)}.");

        RuleFor(x => x.HandoverReason)
            .MaximumLength(255).WithMessage("Lý do bàn giao không được vượt quá 255 ký tự.");

        RuleFor(x => x.HandoverLocation)
            .MaximumLength(200).WithMessage("Địa điểm bàn giao không được vượt quá 200 ký tự.");

        RuleFor(x => x.HandoverNote)
            .MaximumLength(500).WithMessage("Ghi chú bàn giao không được vượt quá 500 ký tự.");
    }
}
