namespace SelfStorageSystem.Contracts.Customer.Rentals;

public record RequestMoveOutRequest(
    DateOnly RequestedMoveOutDate,
    string? Reason = null
);

public record MoveOutResponseDto(
    long Id,
    long AgreementId,
    DateOnly RequestedMoveOutDate,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt
);

public record RefundPreviewDto(
    long AgreementId,
    string AgreementNo,
    decimal DepositBalance,
    decimal EstimatedCleaningFee,
    decimal EstimatedRepairFee,
    decimal EstimatedOverdueCharges,
    decimal EstimatedNetRefund,
    string Note
);

public record RenewAgreementRequest(
    int RenewalMonths
);

public record RenewAgreementResponseDto(
    long RenewalId,
    long AgreementId,
    DateOnly OldEndDate,
    DateOnly NewEndDate,
    decimal MonthlyRate,
    decimal TotalRenewalAmount,
    string Status,
    DateTimeOffset CreatedAt
);

public record CreateAuthorizedMemberRequest(
    string FullName,
    string? IdentityFingerprint,
    string? RelationshipToCustomer,
    DateTimeOffset? ValidTo = null
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
    DateTimeOffset CreatedAt
);

public record CreateTransferRequest(
    long RequestedUnitTypeId,
    DateOnly RequestedEffectiveDate,
    string Reason
);

public record UnitTransferRequestDto(
    long Id,
    long AgreementId,
    long FromUnitId,
    string? FromUnitCode,
    long RequestedUnitTypeId,
    string? RequestedUnitTypeName,
    DateOnly RequestedEffectiveDate,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt
);
