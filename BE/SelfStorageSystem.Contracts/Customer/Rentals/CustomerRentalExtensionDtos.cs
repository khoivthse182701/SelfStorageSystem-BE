using System.Text.Json.Serialization;

namespace SelfStorageSystem.Contracts.Customer.Rentals;

public record RequestMoveOutRequest(
    DateOnly RequestedMoveOutDate,
    string? Reason = null,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankAccountHolder = null,
    string? BankAccountNo = null
)
{
    [JsonIgnore]
    public string? ResolvedBankAccountNo => !string.IsNullOrWhiteSpace(BankAccountNo) ? BankAccountNo.Trim() : BankAccountNumber?.Trim();
}

public record MoveOutResponseDto(
    long Id,
    long AgreementId,
    DateOnly RequestedMoveOutDate,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    long? MoveOutRequestId = null,
    string? MoveOutQrToken = null,
    string? BankName = null,
    string? BankAccountNo = null,
    string? BankAccountHolder = null
)
{
    public long ResolvedMoveOutRequestId => MoveOutRequestId ?? Id;
}

public record RefundPreviewDto(
    long AgreementId,
    string AgreementNo,
    decimal DepositBalance,
    decimal EstimatedCleaningFee,
    decimal EstimatedRepairFee,
    decimal EstimatedOverdueCharges,
    decimal EstimatedNetRefund,
    string Note,
    decimal OriginalDeposit = 0m,
    decimal UnpaidDebt = 0m,
    decimal EstimatedUnusedRentRefund = 0m,
    decimal EarlyTerminationFee = 0m
);

public record RenewAgreementRequest(
    int RenewalMonths = 0,
    int? DurationMonths = null,
    string? PromotionCode = null
)
{
    [JsonIgnore]
    public int ResolvedMonths => DurationMonths ?? RenewalMonths;
}

public record RenewAgreementResponseDto(
    long RenewalId,
    long AgreementId,
    DateOnly OldEndDate,
    DateOnly NewEndDate,
    decimal MonthlyRate,
    decimal TotalRenewalAmount,
    string Status,
    DateTimeOffset CreatedAt,
    long? InvoiceId = null,
    string? InvoiceNo = null,
    DateOnly? RequestedEndDate = null,
    decimal OverdueLateFeeAmount = 0m,
    decimal TotalDueAmount = 0m,
    bool CheckoutRequired = true
);

public record CreateAuthorizedMemberRequest(
    string FullName,
    string? IdentityFingerprint = null,
    string? RelationshipToCustomer = null,
    DateTimeOffset? ValidTo = null,
    bool IssuePin = false
);

public record AuthorizedMemberDto(
    long Id,
    long AgreementId,
    string FullName,
    string? IdentityFingerprint,
    string? RelationshipToCustomer,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Status,
    DateTimeOffset CreatedAt,
    string? IssuedMemberPin = null
);

public record CreateTransferRequest(
    long RequestedUnitTypeId,
    long RequestedFacilityId,
    DateOnly MoveOutDate,
    DateOnly MoveInDate,
    string Reason
);

public record RescheduleTransferRequest(
    DateOnly MoveOutDate,
    DateOnly MoveInDate
);

public record UnitTransferRequestDto(
    long Id,
    long AgreementId,
    long FromUnitId,
    string? FromUnitCode,
    long RequestedUnitTypeId,
    string? RequestedUnitTypeName,
    long RequestedFacilityId,
    DateOnly MoveOutDate,
    DateOnly MoveInDate,
    decimal DiffRentAmount,
    decimal DiffDepositAmount,
    decimal CleaningFee,
    decimal TotalPaidAmount,
    int RescheduleCount,
    long? InvoiceId,
    string? PaymentQrUrl,
    string? PaymentContent,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    string? PaymentBankCode = null,
    string? PaymentAccountNo = null,
    string? PaymentAccountName = null,
    decimal? AmountDue = null,
    decimal? PaidAmount = null,
    DateTimeOffset? HoldUntil = null
);

public record TransferPreviewDto(
    long AgreementId,
    string AgreementNo,
    long CurrentFacilityId,
    string CurrentFacilityName,
    long CurrentUnitTypeId,
    string CurrentUnitTypeName,
    decimal CurrentMonthlyRate,
    decimal CurrentDeposit,
    int TransferCount,
    int MaxTransfers,
    bool CanTransfer,
    string? DisabledReason,
    long? TargetFacilityId,
    string? TargetFacilityName,
    long? TargetUnitTypeId,
    string? TargetUnitTypeName,
    decimal? TargetMonthlyRate,
    decimal? TargetDeposit,
    decimal DiffRentAmount,
    decimal DiffDepositAmount,
    decimal CleaningFee,
    decimal TotalRequiredPayment,
    bool IsDowngrade,
    bool IsCrossFacility,
    int AvailableUnitsCount = 0
);
