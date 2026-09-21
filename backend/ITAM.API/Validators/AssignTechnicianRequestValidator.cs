using FluentValidation;
using ITAM.API.Models.DTOs.MaintenanceTickets;

namespace ITAM.API.Validators;

public class AssignTechnicianRequestValidator : AbstractValidator<AssignTechnicianRequestDto>
{
    public AssignTechnicianRequestValidator()
    {
        RuleFor(x => x.TechnicianId).GreaterThan(0).WithMessage("Phải chọn kỹ thuật viên.");
    }
}
