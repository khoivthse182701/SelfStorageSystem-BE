using FluentValidation;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;

namespace SelfStorageSystem.Application.Validators;

public sealed class VerifyCheckInQrRequestValidator : AbstractValidator<VerifyCheckInQrRequest>
{
    public VerifyCheckInQrRequestValidator()
    {
        RuleFor(x => x.QrToken).NotEmpty().WithMessage("QR token is required.");
        RuleFor(x => x.FacilityId).GreaterThan(0).WithMessage("Facility ID must be greater than 0.");
    }
}

public sealed class AssignReservationUnitRequestValidator : AbstractValidator<AssignReservationUnitRequest>
{
    public AssignReservationUnitRequestValidator()
    {
        RuleFor(x => x.StorageUnitId).GreaterThan(0).WithMessage("Storage unit ID must be greater than 0.");
        RuleFor(x => x.Reason).MaximumLength(255).WithMessage("Reason cannot exceed 255 characters.");
    }
}

public sealed class ConfirmCheckInRequestValidator : AbstractValidator<ConfirmCheckInRequest>
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "government_id", "passport", "digital_identity", "manual"
    };

    private static readonly HashSet<string> AllowedOverallConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "good", "acceptable", "damaged", "unsafe"
    };

    private static readonly HashSet<string> AllowedItemConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "good", "acceptable", "damaged", "missing", "not_applicable"
    };

    public ConfirmCheckInRequestValidator()
    {
        RuleFor(x => x.ReservationId).GreaterThan(0).WithMessage("Reservation ID must be greater than 0.");
        RuleFor(x => x.OverrideUnitId).GreaterThan(0).When(x => x.OverrideUnitId.HasValue);

        RuleFor(x => x.IdentityVerification).NotNull().WithMessage("Identity verification details are required.");
        When(x => x.IdentityVerification is not null, () =>
        {
            RuleFor(x => x.IdentityVerification.Method)
                .NotEmpty()
                .Must(m => AllowedMethods.Contains(m.Trim()))
                .WithMessage("Identity verification method must be government_id, passport, digital_identity, or manual.");

            RuleFor(x => x.IdentityVerification.DocumentNumber)
                .NotEmpty().WithMessage("Customer ID/passport document number is required.")
                .MaximumLength(64);

            RuleFor(x => x.IdentityVerification.Notes)
                .MaximumLength(255);
        });

        RuleFor(x => x.Inspection).NotNull().WithMessage("Check-in inspection details are required.");
        When(x => x.Inspection is not null, () =>
        {
            RuleFor(x => x.Inspection.OverallCondition)
                .NotEmpty()
                .Must(c => AllowedOverallConditions.Contains(c.Trim()))
                .WithMessage("Overall condition must be good, acceptable, damaged, or unsafe.");

            RuleFor(x => x.Inspection.Summary)
                .MaximumLength(255);

            RuleForEach(x => x.Inspection.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ItemName).NotEmpty().MaximumLength(150);
                item.RuleFor(i => i.Condition)
                    .NotEmpty()
                    .Must(c => AllowedItemConditions.Contains(c.Trim()))
                    .WithMessage("Item condition must be good, acceptable, damaged, missing, or not_applicable.");
                item.RuleFor(i => i.Notes).MaximumLength(255);
                item.RuleFor(i => i.PhotoUrl).MaximumLength(255);
            });
        });

        RuleFor(x => x.CustomerSignatureRef).MaximumLength(255);
        RuleFor(x => x.StaffSignatureRef).MaximumLength(255);
        RuleFor(x => x.Notes).MaximumLength(255);
    }
}

public sealed class StaffCreateCheckInIncidentTicketRequestValidator : AbstractValidator<StaffCreateCheckInIncidentTicketRequest>
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "normal", "high", "urgent"
    };

    public StaffCreateCheckInIncidentTicketRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReservationId).GreaterThan(0).When(x => x.ReservationId.HasValue);
        RuleFor(x => x.AgreementId).GreaterThan(0).When(x => x.AgreementId.HasValue);
        RuleFor(x => x.StorageUnitId).GreaterThan(0).When(x => x.StorageUnitId.HasValue);
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Incident description is required.")
            .MaximumLength(255);
        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || AllowedPriorities.Contains(p.Trim()))
            .WithMessage("Priority must be low, normal, high, or urgent.");
    }
}

public sealed class ManagerReassignUnitRequestValidator : AbstractValidator<ManagerReassignUnitRequest>
{
    public ManagerReassignUnitRequestValidator()
    {
        RuleFor(x => x.NewStorageUnitId).GreaterThan(0).WithMessage("New storage unit ID must be greater than 0.");
        RuleFor(x => x.TicketId).GreaterThan(0).When(x => x.TicketId.HasValue);
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(255);
    }
}

public sealed class ManagerEmergencyCancelRefundRequestValidator : AbstractValidator<ManagerEmergencyCancelRefundRequest>
{
    public ManagerEmergencyCancelRefundRequestValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0).When(x => x.TicketId.HasValue);
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(200);
    }
}
