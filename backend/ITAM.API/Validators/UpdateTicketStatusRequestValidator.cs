using FluentValidation;
using ITAM.API.Models.DTOs.MaintenanceTickets;

namespace ITAM.API.Validators;

public class UpdateTicketStatusRequestValidator : AbstractValidator<UpdateTicketStatusRequestDto>
{
    public UpdateTicketStatusRequestValidator()
    {
        // Pending không phải trạng thái hợp lệ để chuyển tới (UC-12) — khác validator của Asset.
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái không được để trống.")
            .Must(s => s is "Resolved" or "Failed").WithMessage("Trạng thái chỉ được là Resolved hoặc Failed.");
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Ghi chú không được vượt quá 1000 ký tự.");
    }
}
