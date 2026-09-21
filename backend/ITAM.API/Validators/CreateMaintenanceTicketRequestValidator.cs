using FluentValidation;
using ITAM.API.Models.DTOs.MaintenanceTickets;

namespace ITAM.API.Validators;

public class CreateMaintenanceTicketRequestValidator : AbstractValidator<CreateMaintenanceTicketRequestDto>
{
    public CreateMaintenanceTicketRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("Phải chọn tài sản.");
        RuleFor(x => x.IssueDescription)
            .NotEmpty().WithMessage("Mô tả lỗi không được để trống.")
            .MaximumLength(1000).WithMessage("Mô tả lỗi không được vượt quá 1000 ký tự.");
        RuleFor(x => x.TechnicianId).GreaterThan(0).When(x => x.TechnicianId.HasValue)
            .WithMessage("TechnicianId không hợp lệ.");
    }
}
