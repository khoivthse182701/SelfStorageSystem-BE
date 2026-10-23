namespace SelfStorageSystem.Contracts.Staff;

public record StaffFacilityInfoDto(
    long FacilityId,
    string FacilityName,
    string FacilityCode,
    string? Address,
    string? City,
    int TotalUnits
);

// ==========================================
// 1. UNIT AVAILABILITY & DETAILS (Nhóm 1)
// ==========================================

public record StaffUnitGridDto(
    long UnitId,
    string UnitCode,
    long FacilityId,
    string FacilityName,
    long UnitTypeId,
    string UnitTypeName,
    decimal WidthM,
    decimal LengthM,
    decimal HeightM,
    decimal? AreaM2,
    decimal? VolumeM3,
    string PhysicalStatus,
    bool IsListed,
    string? FloorLabel,
    string? ZoneLabel,
    decimal? CurrentRate,
    string? CurrentAgreementNo,
    string? CustomerName,
    StaffUnitMapPositionDto? MapPosition
);

public record StaffUnitMapPositionDto(
    long AreaId,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    decimal RotationDegrees
);

public record StaffUnitDetailDto(
    long UnitId,
    string UnitCode,
    long FacilityId,
    string FacilityName,
    string? FloorLabel,
    string? ZoneLabel,
    string PhysicalStatus,
    bool IsListed,
    string? Notes,
    long UnitTypeId,
    string UnitTypeName,
    decimal WidthM,
    decimal LengthM,
    decimal HeightM,
    decimal? AreaM2,
    decimal? VolumeM3,
    bool ClimateControlled,
    decimal MonthlyRate,
    decimal DepositAmount,
    decimal BookingFee,
    StaffCurrentAgreementInfoDto? CurrentAgreement
);

public record StaffCurrentAgreementInfoDto(
    long AgreementId,
    string AgreementNo,
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status
);

// ==========================================
// 2. WALK-IN BOOKINGS AT COUNTER (Nhóm 2)
// ==========================================

public record StaffWalkInBookingRequest(
    string CustomerPhone,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerIdentityNumber,
    long UnitId,
    DateOnly StartDate,
    int DurationMonths,
    string? Notes = null
);

public record StaffWalkInBookingResponse(
    long ReservationId,
    string ReservationCode,
    long InvoiceId,
    string InvoiceNo,
    long CustomerId,
    string CustomerName,
    string CustomerPhone,
    long UnitId,
    string UnitCode,
    long FacilityId,
    string FacilityName,
    decimal MonthlyRent,
    decimal DepositAmount,
    decimal TotalAmount,
    string QrImageUrl,
    string TransferContent,
    string BankCode,
    string AccountNo,
    string AccountName,
    string Status
);

// ==========================================
// 3. QR VERIFICATION & CONFIRM ACTION (Nhóm 3)
// ==========================================

public record StaffQrVerifyRequest(
    string QrCode
);

public record StaffQrVerifyResponse(
    string ActionType, // "check_in" | "transfer_move_out" | "transfer_move_in" | "move_out" | "gate_access"
    string Title,      // e.g. "Khách Nguyễn Văn A - Nhận kho HN"
    string Description,
    StaffCustomerSummaryDto Customer,
    StaffAgreementSummaryDto? Agreement,
    StaffReservationSummaryDto? Reservation,
    StaffTransferSummaryDto? TransferRequest,
    StaffUnitSummaryDto? Unit,
    List<string> AllowedActions
);

public record StaffCustomerSummaryDto(
    long CustomerId,
    string FullName,
    string? PhoneNumber,
    string? Email
);

public record StaffAgreementSummaryDto(
    long AgreementId,
    string AgreementNo,
    long FacilityId,
    string FacilityName,
    long UnitId,
    string UnitCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status
);

public record StaffReservationSummaryDto(
    long ReservationId,
    string ReservationCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status
);

public record StaffTransferSummaryDto(
    long TransferRequestId,
    long AgreementId,
    string AgreementNo,
    long FromFacilityId,
    string FromFacilityName,
    long ToFacilityId,
    string ToFacilityName,
    long FromUnitId,
    string FromUnitCode,
    long? ToUnitId,
    string? ToUnitCode,
    string Status
);

public record StaffUnitSummaryDto(
    long UnitId,
    string UnitCode,
    long FacilityId,
    string FacilityName,
    string UnitTypeName,
    string PhysicalStatus
);

public record StaffConfirmQrActionRequest(
    string ActionType,
    long? ReservationId = null,
    long? AgreementId = null,
    long? TransferRequestId = null,
    long? MoveOutId = null,
    long? UnitId = null,
    string? Notes = null
);

public record StaffConfirmQrActionResultDto(
    bool Success,
    string ActionType,
    string Message,
    long? AgreementId,
    string? AgreementStatus,
    long? UnitId,
    string? UnitStatus
);

// ==========================================
// 4. LOGISTICS & MAINTENANCE TICKETS (Nhóm 4)
// ==========================================

public record StaffLogisticsTicketDto(
    long Id,
    string TicketNo,
    long FacilityId,
    string FacilityName,
    long? StorageUnitId,
    string? UnitCode,
    long CustomerId,
    string CustomerName,
    long? AgreementId,
    string? AgreementNo,
    string Category,
    string Priority,
    string Subject,
    string Description,
    string Status,
    long? AssignedEmployeeId,
    string? AssignedEmployeeName,
    string? Resolution,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasProposedCharges
);

public record StaffProposeChargeRequest(
    decimal Amount,
    string Reason,
    string? Description = null,
    string? PhotoUrl = null
);

public record StaffCompleteTicketRequest(
    string? Resolution = null
);

public record StaffCompleteTicketResultDto(
    long TicketId,
    string TicketNo,
    string Status,
    string? Resolution,
    DateTimeOffset ResolvedAt,
    long? UnlockedUnitId,
    string? UnlockedUnitCode,
    string? UnlockedUnitStatus
);

// ==========================================
// 5. SHIFTS SCHEDULE
// ==========================================

public record StaffShiftScheduleDto(
    long ShiftId,
    long FacilityId,
    string FacilityName,
    string ShiftName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    long EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string DutyRole,
    string Status,
    DateTimeOffset? CheckInAt,
    DateTimeOffset? CheckOutAt
);

// ==========================================
// LEGACY / BACKWARD-COMPATIBILITY DTOS
// ==========================================

public record StaffTaskDto(
    long Id,
    long FacilityId,
    string FacilityCode,
    long? AssignedEmployeeId,
    string? AssignedEmployeeName,
    string TaskType,
    string Title,
    DateTimeOffset? DueAt,
    string Status,
    short ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record UpdateStaffTaskStatusRequest(
    string Status,
    short? ProgressPercent = null
);

public record StaffReservationLookupDto(
    long ReservationId,
    string ReservationCode,
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    long FacilityId,
    string FacilityCode,
    long UnitTypeId,
    string UnitTypeName,
    long? AssignedUnitId,
    string? AssignedUnitCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    decimal QuotedTotal,
    decimal DepositSnapshot,
    DateTimeOffset CreatedAt
);

public record AssignUnitToReservationRequest(
    long StorageUnitId
);

public record CreateHandoverInspectionItemRequest(
    string ItemName,
    string Condition,
    string? Notes = null,
    string? PhotoUrl = null,
    decimal ChargeAmount = 0m
);

public record CreateStaffHandoverRequest(
    long ReservationId,
    string HandoverType,
    string? CustomerSignatureRef,
    string? StaffSignatureRef,
    string? Notes,
    string? OverallCondition,
    string? InspectionSummary,
    List<CreateHandoverInspectionItemRequest>? InspectionItems = null
);

public record StaffHandoverResultDto(
    long HandoverId,
    long AgreementId,
    string AgreementNo,
    long ReservationId,
    long StorageUnitId,
    string UnitCode,
    string HandoverType,
    string AgreementStatus,
    DateTimeOffset HandoverTime
);

public record StaffMoveOutSummaryDto(
    long Id,
    long AgreementId,
    string AgreementNo,
    long CustomerId,
    string CustomerName,
    long FacilityId,
    string UnitCode,
    DateOnly RequestedMoveOutDate,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt
);

public record InspectMoveOutRequest(
    string OverallCondition,
    string? Summary,
    List<CreateHandoverInspectionItemRequest>? InspectionItems,
    string NextUnitStatus = "available"
);

public record InspectMoveOutResultDto(
    long MoveOutId,
    long InspectionId,
    long AgreementId,
    decimal TotalDamageCharges,
    string AgreementStatus,
    string UnitStatus,
    DateTimeOffset InspectedAt
);

public record StaffSupportTicketDto(
    long Id,
    string TicketNo,
    long FacilityId,
    string FacilityCode,
    long CustomerId,
    string CustomerName,
    long? AgreementId,
    string? AgreementNo,
    long? StorageUnitId,
    string? UnitCode,
    string Category,
    string Priority,
    string Subject,
    string Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record StaffTicketMessageDto(
    long Id,
    long TicketId,
    long? AuthorUserId,
    string? AuthorName,
    string? AuthorRole,
    string Body,
    bool IsInternal,
    DateTimeOffset CreatedAt
);

public record AddStaffTicketMessageRequest(
    string Body,
    bool IsInternal = false
);

public record ProposeTicketChargeRequest(
    string Description,
    decimal Amount
);

public record TicketChargeProposalDto(
    long Id,
    long TicketId,
    long ProposedBy,
    string? ProposedByName,
    string Description,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt
);

public record ResolveTicketRequest(
    string Resolution
);

public record StaffFacilityUnitDto(
    long UnitId,
    string UnitCode,
    long UnitTypeId,
    string UnitTypeName,
    string Status,
    decimal? CurrentRate,
    string? CurrentAgreementNo,
    string? CustomerName
);

public record UpdateUnitStatusRequest(
    string Status
);

public record CreateMaintenanceWorkOrderRequest(
    string Title,
    string Description,
    string Priority,
    bool BlocksBooking,
    decimal? EstimatedCost
);

public record MaintenanceWorkOrderDto(
    long Id,
    string WorkOrderNo,
    long FacilityId,
    long? StorageUnitId,
    string? UnitCode,
    string Title,
    string Description,
    string Priority,
    bool BlocksBooking,
    string Status,
    decimal? EstimatedCost,
    DateTimeOffset CreatedAt
);

public record OverdueAgreementDto(
    long AgreementId,
    string AgreementNo,
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    long StorageUnitId,
    string UnitCode,
    decimal OutstandingBalance,
    DateOnly StartDate,
    DateOnly EndDate,
    string AgreementStatus,
    string CredentialStatus
);

public record LockAccessRequest(
    string Reason
);
