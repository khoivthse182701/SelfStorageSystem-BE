using FluentValidation;
using SelfStorageSystem.Contracts.Customer;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Staff;

namespace SelfStorageSystem.Application.Validators;

public sealed class ChangePinRequestValidator : AbstractValidator<ChangePinRequest>
{
    public ChangePinRequestValidator()
    {
        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("New PIN is required.")
            .Matches(@"^\d{6}$").WithMessage("PIN must be exactly 6 numeric digits.");

        RuleFor(x => x.CurrentPin)
            .MaximumLength(16)
            .When(x => !string.IsNullOrEmpty(x.CurrentPin));
    }
}

public sealed class UnlockStorageUnitRequestValidator : AbstractValidator<UnlockStorageUnitRequest>
{
    public UnlockStorageUnitRequestValidator()
    {
        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{6}$").WithMessage("PIN must be exactly 6 numeric digits.");

        RuleFor(x => x.AccessPointId)
            .GreaterThan(0)
            .When(x => x.AccessPointId.HasValue);
    }
}

public sealed class CreateAuthorizedMemberRequestValidator : AbstractValidator<CreateAuthorizedMemberRequest>
{
    public CreateAuthorizedMemberRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Member full name is required.")
            .MaximumLength(150);

        RuleFor(x => x.IdentityFingerprint)
            .MaximumLength(100)
            .When(x => x.IdentityFingerprint != null);

        RuleFor(x => x.RelationshipToCustomer)
            .MaximumLength(80)
            .When(x => x.RelationshipToCustomer != null);
    }
}

public sealed class DeclareStoredItemsRequestValidator : AbstractValidator<DeclareStoredItemsRequest>
{
    public DeclareStoredItemsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one stored item must be provided.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemName).NotEmpty().MaximumLength(150);
            item.RuleFor(i => i.Category).NotEmpty().MaximumLength(64);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.EstimatedValue).GreaterThanOrEqualTo(0m).When(i => i.EstimatedValue.HasValue);
            item.RuleFor(i => i.PhotoUrl).MaximumLength(500).When(i => i.PhotoUrl != null);
            item.RuleFor(i => i.LocationNote).MaximumLength(255).When(i => i.LocationNote != null);
        });
    }
}

public sealed class UpdateStoredItemRequestValidator : AbstractValidator<UpdateStoredItemRequest>
{
    public UpdateStoredItemRequestValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.EstimatedValue).GreaterThanOrEqualTo(0m).When(x => x.EstimatedValue.HasValue);
        RuleFor(x => x.PhotoUrl).MaximumLength(500).When(x => x.PhotoUrl != null);
        RuleFor(x => x.LocationNote).MaximumLength(255).When(x => x.LocationNote != null);
    }
}

public sealed class RequestMoveOutRequestValidator : AbstractValidator<RequestMoveOutRequest>
{
    public RequestMoveOutRequestValidator()
    {
        RuleFor(x => x.RequestedMoveOutDate)
            .NotEqual(default(DateOnly)).WithMessage("Requested move-out date is required.");

        RuleFor(x => x.Reason).MaximumLength(250).When(x => x.Reason != null);
    }
}

public sealed class CustomerSubmitRefundBankInfoRequestValidator : AbstractValidator<CustomerSubmitRefundBankInfoRequest>
{
    public CustomerSubmitRefundBankInfoRequestValidator()
    {
        RuleFor(x => x.BankName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.BankAccountNumber).NotEmpty().MinimumLength(4).MaximumLength(50);
        RuleFor(x => x.BankAccountHolder).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Note).MaximumLength(255).When(x => x.Note != null);
    }
}

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        RuleFor(x => x.RequestedFacilityId).GreaterThan(0);
        RuleFor(x => x.RequestedUnitTypeId).GreaterThan(0);
        RuleFor(x => x.MoveOutDate).NotEqual(default(DateOnly));
        RuleFor(x => x.MoveInDate).GreaterThanOrEqualTo(x => x.MoveOutDate);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class RescheduleTransferRequestValidator : AbstractValidator<RescheduleTransferRequest>
{
    public RescheduleTransferRequestValidator()
    {
        RuleFor(x => x.MoveOutDate).NotEqual(default(DateOnly));
        RuleFor(x => x.MoveInDate).GreaterThanOrEqualTo(x => x.MoveOutDate);
    }
}

public sealed class InspectMoveOutRequestValidator : AbstractValidator<InspectMoveOutRequest>
{
    public InspectMoveOutRequestValidator()
    {
        RuleFor(x => x.OverallCondition).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Summary).MaximumLength(500).When(x => x.Summary != null);

        When(x => x.DamageItems != null, () =>
        {
            RuleForEach(x => x.DamageItems!).ChildRules(item =>
            {
                item.RuleFor(i => i.ItemName).NotEmpty().MaximumLength(150);
                item.RuleFor(i => i.Condition).NotEmpty().MaximumLength(40);
                item.RuleFor(i => i.ChargeAmount).GreaterThanOrEqualTo(0m);
                item.RuleFor(i => i.Cost).GreaterThanOrEqualTo(0m);
            });
        });
    }
}

public sealed class StaffSubmitInspectionReportRequestValidator : AbstractValidator<StaffSubmitInspectionReportRequest>
{
    public StaffSubmitInspectionReportRequestValidator()
    {
        RuleFor(x => x.DamageCost).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OverallCondition).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Summary).MaximumLength(500).When(x => x.Summary != null);
    }
}

public sealed class StaffCounterCashPayoutRequestValidator : AbstractValidator<StaffCounterCashPayoutRequest>
{
    public StaffCounterCashPayoutRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note != null);
        RuleFor(x => x.ReceiptReference).MaximumLength(120).When(x => x.ReceiptReference != null);
        RuleFor(x => x.ProofImageUrl).MaximumLength(500).When(x => x.ProofImageUrl != null);
    }
}

public sealed class CreateStaffAccessEventRequestValidator : AbstractValidator<CreateStaffAccessEventRequest>
{
    private static readonly HashSet<string> AllowedResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "granted", "denied", "error"
    };

    public CreateStaffAccessEventRequestValidator()
    {
        RuleFor(x => x.AccessPointId).GreaterThan(0);
        RuleFor(x => x.Result)
            .NotEmpty()
            .Must(r => AllowedResults.Contains(r.Trim()))
            .WithMessage("Access event result must be granted, denied, or error.");
        RuleFor(x => x.Reason).MaximumLength(255).When(x => x.Reason != null);
    }
}
