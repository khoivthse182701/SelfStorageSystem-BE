namespace SelfStorageSystem.Contracts.Staff;

public sealed record StaffAccessEventDto(
    long Id,
    long FacilityId,
    long? AgreementId,
    string? AgreementNo,
    long? StorageUnitId,
    string? UnitCode,
    long AccessPointId,
    string AccessPointName,
    string AccessPointType,
    long? CredentialId,
    string ActorName,
    string Result,
    string? Reason,
    DateTimeOffset OccurredAt
);

public sealed record CreateStaffAccessEventRequest(
    long AccessPointId,
    long? CredentialId,
    long? AgreementId,
    string Result,
    string? Reason = null
);

public sealed record StaffLockAccessResponseDto(
    long AgreementId,
    int SuspendedCredentialsCount,
    DateTimeOffset LockedAt
);
