using FluentValidation;
using ITAM.API.Models.DTOs.MaintenanceTickets;
using ITAM.API.Models.Enums;

namespace ITAM.API.Validators;

public class UpdateTicketPriorityRequestValidator : AbstractValidator<UpdateTicketPriorityRequestDto>
{
    public UpdateTicketPriorityRequestValidator()
    {
        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Phải chọn mức độ khẩn.")
            .Must(p => Enum.GetNames<TicketPriority>().Contains(p, StringComparer.Ordinal))
            .WithMessage($"Mức độ khẩn không hợp lệ. Phải là một trong: {string.Join(", ", Enum.GetNames<TicketPriority>())}.");
    }
}
