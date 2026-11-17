using FluentValidation;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Application.Validators;

public sealed class RenewAgreementRequestValidator : AbstractValidator<RenewAgreementRequest>
{
    public RenewAgreementRequestValidator()
    {
        RuleFor(x => x.ResolvedMonths)
            .InclusiveBetween(1, 12)
            .WithMessage("Renewal duration must be between 1 and 12 months.");

        RuleFor(x => x.PromotionCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.PromotionCode));
    }
}

public sealed class LockAccessRequestValidator : AbstractValidator<LockAccessRequest>
{
    public LockAccessRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason != null);
    }
}

public sealed class ManagerAddDelinquencyActionRequestValidator : AbstractValidator<ManagerAddDelinquencyActionRequest>
{
    public ManagerAddDelinquencyActionRequestValidator()
    {
        RuleFor(x => x.ActionType)
            .NotEmpty()
            .Must(a => DelinquencyActionTypeConstants.All.Contains(a.Trim().ToLowerInvariant()))
            .WithMessage("Invalid delinquency action type.");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note != null);

        RuleFor(x => x.LateFeeAmount)
            .GreaterThan(0m)
            .When(x => x.LateFeeAmount.HasValue);
    }
}

public sealed class ManagerLiquidateAgreementRequestValidator : AbstractValidator<ManagerLiquidateAgreementRequest>
{
    public ManagerLiquidateAgreementRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Liquidation reason is required.")
            .MaximumLength(500);

        RuleFor(x => x.AuctionProceedsAmount)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.AuctionProceedsAmount.HasValue);
    }
}

public sealed class ManagerAssignInspectionRequestValidator : AbstractValidator<ManagerAssignInspectionRequest>
{
    public ManagerAssignInspectionRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0).WithMessage("Assigned employee ID must be greater than 0.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes != null);
    }
}
