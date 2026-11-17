using FluentValidation;
using SelfStorageSystem.Contracts.Customer.Facilities;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Contracts.Customer.Reservations;

namespace SelfStorageSystem.Application.Validators;

public sealed class PricingQuoteRequestValidator : AbstractValidator<PricingQuoteRequest>
{
    public PricingQuoteRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0).WithMessage("Facility ID must be greater than 0.");
        RuleFor(x => x.UnitTypeId).GreaterThan(0).WithMessage("Unit type ID must be greater than 0.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.DurationMonths).InclusiveBetween(1, 12).WithMessage("Duration must be between 1 and 12 months.");
        RuleFor(x => x.PromotionCode).MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.PromotionCode));
    }
}

public sealed class CalculatePricingRequestValidator : AbstractValidator<CalculatePricingRequest>
{
    public CalculatePricingRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0).WithMessage("Facility ID must be greater than 0.");
        RuleFor(x => x.UnitTypeId).GreaterThan(0).WithMessage("Unit type ID must be greater than 0.");
        RuleFor(x => x.DurationMonths).InclusiveBetween(1, 12).WithMessage("Duration must be between 1 and 12 months.");
    }
}

public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.FacilityId).GreaterThan(0).WithMessage("Facility ID is required.");
        RuleFor(x => x.UnitTypeId).GreaterThan(0).WithMessage("Unit type ID is required.");
        RuleFor(x => x.StorageUnitId).GreaterThan(0).When(x => x.StorageUnitId.HasValue)
            .WithMessage("Storage unit ID must be greater than 0.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.DurationMonths).InclusiveBetween(1, 12)
            .WithMessage("Rental duration must be between 1 and 12 months.");
        RuleFor(x => x.PromotionCode).MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.PromotionCode));
        RuleFor(x => x.IdempotencyKey).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));
    }
}

public sealed class CancelReservationRequestValidator : AbstractValidator<CancelReservationRequest>
{
    public CancelReservationRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(255).WithMessage("Cancellation reason cannot exceed 255 characters.");
    }
}

public sealed class CreateCheckoutRequestValidator : AbstractValidator<CreateCheckoutRequest>
{
    public CreateCheckoutRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => CountTargets(x) == 1)
            .WithName("targetId")
            .WithMessage("Exactly one positive target ID (reservationId, invoiceId, or renewalId) is required.");

        RuleFor(x => x.ReservationId).GreaterThan(0).When(x => x.ReservationId.HasValue);
        RuleFor(x => x.InvoiceId).GreaterThan(0).When(x => x.InvoiceId.HasValue);
        RuleFor(x => x.RenewalId).GreaterThan(0).When(x => x.RenewalId.HasValue);

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .Must(m => string.Equals(m, "bank_transfer", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(m, "sepay_vietqr", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Supported payment methods are 'bank_transfer' and 'sepay_vietqr'.");
    }

    private static int CountTargets(CreateCheckoutRequest req) =>
        (req.ReservationId.HasValue ? 1 : 0) +
        (req.InvoiceId.HasValue ? 1 : 0) +
        (req.RenewalId.HasValue ? 1 : 0);
}

public sealed class SePayWebhookPayloadValidator : AbstractValidator<SePayWebhookPayload>
{
    public SePayWebhookPayloadValidator()
    {
        RuleFor(x => x.TransferAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Transfer amount cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.Id > 0 || !string.IsNullOrWhiteSpace(x.ReferenceCode) || !string.IsNullOrWhiteSpace(x.Content))
            .WithMessage("Webhook transaction payload must contain an ID, reference code, or transfer content.");
    }
}
