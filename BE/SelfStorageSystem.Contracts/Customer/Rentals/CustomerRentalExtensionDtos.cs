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
    decimal TotalPaidAmount,
    int RescheduleCount,
    long? InvoiceId,
    string? PaymentQrUrl,
    string? PaymentContent,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt
);
