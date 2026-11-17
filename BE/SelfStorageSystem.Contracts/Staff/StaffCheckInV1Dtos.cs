namespace SelfStorageSystem.Contracts.Staff;

public sealed record VerifyCheckInQrRequest(
    string QrToken,
    long FacilityId,
    string? CustomerIdentityNumber = null);

public sealed record BackupStorageUnitDto(
    long UnitId,
    string UnitCode,
    long UnitTypeId,
    string UnitTypeName,
    decimal WidthM,
    decimal LengthM,
    decimal HeightM,
    decimal? AreaM2,
    string? FloorLabel,
    string? ZoneLabel,
    string PhysicalStatus);

public sealed record VerifyCheckInQrResponse(
    string ActionType,
    bool Valid,
    StaffCustomerSummaryDto Customer,
    StaffReservationSummaryDto Reservation,
    StaffAgreementSummaryDto? Agreement,
    StaffUnitSummaryDto? Unit,
    List<string> AllowedActions,
    bool IdentityVerified = false,
    List<BackupStorageUnitDto>? AvailableBackupUnits = null);

public sealed record AssignReservationUnitRequest(
    long StorageUnitId,
    string? Reason = null);

public sealed record AssignReservationUnitResponse(
    long ReservationId,
    long? OldUnitId,
    long NewUnitId,
    string NewUnitCode,
    string UnitStatus);

public sealed class ConfirmCheckInRequest
{
    public long ReservationId { get; set; }
    public long? OverrideUnitId { get; set; }
    public IdentityVerificationInput IdentityVerification { get; set; } = new();
    public CheckInInspectionInput Inspection { get; set; } = new();
    public string? CustomerSignatureRef { get; set; }
    public string? StaffSignatureRef { get; set; }
    public string? Notes { get; set; }
}

public sealed class IdentityVerificationInput
{
    public string Method { get; set; } = "government_id";
    public string DocumentNumber { get; set; } = string.Empty;
    public bool FullNameMatched { get; set; }
    public string? Notes { get; set; }
}

public sealed class CheckInInspectionInput
{
    public string OverallCondition { get; set; } = "good";
    public string? Summary { get; set; }
    public List<CheckInInspectionItemInput> Items { get; set; } = new();
}

public sealed class CheckInInspectionItemInput
{
    public string ItemName { get; set; } = string.Empty;
    public string Condition { get; set; } = "good";
    public string? Notes { get; set; }
    public string? PhotoUrl { get; set; }
}

public sealed record ConfirmCheckInResponse(
    long HandoverId,
    long InspectionId,
    long IdentityVerificationId,
    long AgreementId,
    string AgreementNo,
    string AgreementStatus,
    string ReservationStatus,
    long UnitId,
    string UnitCode,
    string UnitStatus,
    string IssuedPin,
    DateTimeOffset CheckedInAt);

public sealed record CreateCheckInTicketResponse(
    long TicketId,
    string TicketNo,
    string Status,
    DateTimeOffset CreatedAt,
    long FacilityId,
    long? StorageUnitId,
    string? UnitCode);
