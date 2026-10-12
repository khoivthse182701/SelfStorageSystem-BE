namespace SelfStorageSystem.Contracts.Staff;

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
    string NextUnitStatus = "available" // available or under_maintenance
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
