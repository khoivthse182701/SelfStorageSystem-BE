namespace SelfStorageSystem.Domain.Enums;

/// <summary>
/// Canonical physical status of a storage unit ([core].[storage_units].[physical_status]).
/// </summary>
public enum UnitStatus
{
    Available = 0,
    Reserved = 1,
    Occupied = 2,
    PendingInspection = 3,
    Cleaning = 4,
    Maintenance = 5,
    Damaged = 6,
    OutOfService = 7
}

/// <summary>
/// Canonical lifecycle status of a reservation ([core].[reservations].[status]).
/// </summary>
public enum ReservationStatus
{
    Pending = 0,
    AwaitingDeposit = 1,
    Confirmed = 2,
    CheckedIn = 3,
    Converted = 4,
    Completed = 5,
    NoShow = 6,
    Cancelled = 7,
    Expired = 8
}

/// <summary>
/// Canonical lifecycle status of a rental agreement/contract ([core].[rental_agreements].[status]).
/// </summary>
public enum ContractStatus
{
    Draft = 0,
    Scheduled = 1,
    Active = 2,
    ExpiringSoon = 3,
    Extended = 4,
    Overdue = 5,
    Defaulted = 6,
    MoveOutScheduled = 7,
    CheckoutPending = 8,
    Expired = 9,
    Terminated = 10,
    Completed = 11,
    Closed = 12,
    Cancelled = 13
}

/// <summary>
/// Canonical status of a payment transaction ([core].[payments].[status]).
/// </summary>
public enum PaymentStatus
{
    Initiated = 0,
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    PartiallyRefunded = 5,
    Refunded = 6
}

/// <summary>
/// Canonical workflow status of a support/issue ticket ([core].[support_tickets].[status]).
/// </summary>
public enum TicketStatus
{
    Open = 0,
    PendingManagerReview = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForCustomer = 4,
    WaitingForMaintenance = 5,
    SevereNeedRelocation = 6,
    Assessed = 7,
    Resolved = 8,
    Closed = 9,
    Cancelled = 10
}

/// <summary>
/// Canonical 4 business roles in the refactored system.
/// </summary>
public enum UserRoleCode
{
    Customer = 1,
    Staff = 2,
    Manager = 3,
    Admin = 4
}

public enum UserStatus
{
    Active = 0,
    Locked = 1,
    Disabled = 2
}

public enum EmploymentStatus
{
    Active = 0,
    Leave = 1,
    Terminated = 2
}

public enum FacilityStatus
{
    Draft = 0,
    Active = 1,
    Inactive = 2,
    Maintenance = 3,
    SuspendedNewBookings = 4,
    TemporarilyClosed = 5,
    Closed = 6
}

public enum AreaType
{
    Building = 0,
    Floor = 1,
    Zone = 2,
    Section = 3
}

public enum InvoiceStatus
{
    Draft = 0,
    Open = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Voided = 5
}

public enum InvoiceLineType
{
    Deposit = 0,
    Rent = 1,
    Renewal = 2,
    LateFee = 3,
    Damage = 4,
    Maintenance = 5,
    BookingFee = 6,
    Discount = 7,
    Penalty = 8,
    Service = 9,
    Fee = 10,
    Tax = 11,
    Other = 12
}

public enum PaymentMethod
{
    Cash = 0,
    BankTransfer = 1,
    CreditCard = 2,
    VnPay = 3,
    PayOs = 4,
    Stripe = 5,
    Other = 6
}

public enum RefundStatus
{
    Requested = 0,
    Pending = 1,
    Approved = 2,
    Processing = 3,
    Succeeded = 4,
    Completed = 5,
    Failed = 6,
    Rejected = 7,
    PendingManagerPayout = 8,
    PendingCounterPayout = 9
}

public enum ApprovalDecision
{
    Approved = 0,
    Rejected = 1
}

public enum FeeType
{
    LateFee = 0,
    DamageFee = 1,
    LostKeyFee = 2,
    MaintenanceFee = 3,
    Booking = 4,
    BookingFee = 5,
    Tax = 6,
    VatTax = 7,
    Other = 8
}

public enum FeeCalculationMethod
{
    Flat = 0,
    Percentage = 1,
    PerDay = 2
}

public enum PromotionDiscountType
{
    Fixed = 0,
    Percentage = 1,
    FreeDays = 2
}

public enum PromotionRuleType
{
    MinimumMonths = 0,
    Month = 1,
    MinimumAmount = 2,
    Facility = 3,
    UnitType = 4,
    NewCustomer = 5
}

public enum PromotionOperator
{
    Eq = 0,
    Gte = 1,
    Lte = 2,
    In = 3
}

public enum PromotionRedemptionStatus
{
    Reserved = 0,
    Applied = 1,
    Released = 2
}

public enum PolicyType
{
    RentalTerms = 0,
    Deposit = 1,
    Cancellation = 2,
    Renewal = 3,
    MoveOut = 4,
    Overdue = 5
}

public enum AllocationKind
{
    ReservationHold = 0,
    Rental = 1,
    Transfer = 2
}

public enum AllocationStatus
{
    Active = 0,
    Consumed = 1,
    Released = 2,
    Expired = 3
}

public enum RenewalStatus
{
    PendingPayment = 0,
    Paid = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum TransferRequestStatus
{
    PendingPayment = 0,
    Paid = 1,
    MovedOutInTransit = 2,
    CancelledOverdueMoveOut = 3,
    Pending = 4,
    Approved = 5,
    Scheduled = 6,
    Completed = 7,
    Rejected = 8,
    Cancelled = 9
}

public enum MoveOutStatus
{
    Requested = 0,
    Scheduled = 1,
    InProgress = 2,
    InspectionPending = 3,
    SettlementPending = 4,
    Completed = 5,
    Cancelled = 6
}

public enum AuthorizedMemberStatus
{
    Pending = 0,
    Active = 1,
    Revoked = 2,
    Expired = 3
}

public enum CredentialType
{
    Pin = 0,
    Card = 1,
    Qr = 2,
    Key = 3,
    Mobile = 4
}

public enum CredentialStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3,
    Expired = 4
}

public enum AccessPointType
{
    Gate = 0,
    BuildingDoor = 1,
    FloorDoor = 2,
    UnitDoor = 3
}

public enum AccessPointStatus
{
    Active = 0,
    Offline = 1,
    Maintenance = 2,
    Retired = 3
}

public enum AccessEventResult
{
    Granted = 0,
    Denied = 1,
    Error = 2
}

public enum DelinquencyCaseStatus
{
    Grace = 0,
    Delinquent = 1,
    Cured = 2,
    Waived = 3,
    TerminationStarted = 4
}

public enum DelinquencyActionType
{
    ReminderSent = 0,
    LateFeeAdded = 1,
    CredentialSuspended = 2,
    CustomerContacted = 3,
    PaymentPlan = 4,
    Waived = 5,
    TerminationStarted = 6,
    Note = 7
}

public enum TicketCategory
{
    Unit = 0,
    Access = 1,
    Payment = 2,
    StoredItem = 3,
    Maintenance = 4,
    Other = 5
}

public enum TicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum ProposalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Invoiced = 3,
    Cancelled = 4
}

public enum InspectionType
{
    CheckIn = 0,
    CheckOut = 1,
    Transfer = 2,
    Security = 3,
    FireSafety = 4,
    Routine = 5,
    Incident = 6
}

public enum InspectionStatus
{
    Draft = 0,
    Completed = 1,
    Failed = 2
}

public enum OverallCondition
{
    Good = 0,
    Acceptable = 1,
    Damaged = 2,
    Unsafe = 3
}

public enum InspectionItemCondition
{
    Good = 0,
    Acceptable = 1,
    Damaged = 2,
    Missing = 3,
    NotApplicable = 4
}

public enum HandoverType
{
    CheckIn = 0,
    CheckOut = 1,
    Transfer = 2
}

public enum IdentityVerificationMethod
{
    GovernmentId = 0,
    Passport = 1,
    DigitalIdentity = 2,
    Manual = 3
}

public enum IdentityVerificationResult
{
    Verified = 0,
    Rejected = 1
}

public enum MaintenanceWorkOrderStatus
{
    Open = 0,
    Assigned = 1,
    InProgress = 2,
    OnHold = 3,
    AwaitingVerification = 4,
    Completed = 5,
    Cancelled = 6
}

public enum StaffShiftStatus
{
    Planned = 0,
    Open = 1,
    Completed = 2,
    Cancelled = 3
}

public enum DutyRole
{
    Staff = 0,
    ShiftLead = 1,
    ManagerOnCall = 2
}

public enum ShiftAssignmentStatus
{
    Scheduled = 0,
    CheckedIn = 1,
    CheckedOut = 2,
    Absent = 3
}

public enum StaffTaskType
{
    CheckIn = 0,
    CheckOut = 1,
    Inspection = 2,
    Maintenance = 3,
    Support = 4,
    Other = 5
}

public enum StaffTaskStatus
{
    Todo = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3,
    Cancelled = 4
}

public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Sms = 2,
    Push = 3
}

public enum NotificationStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}

public enum IntegrationEventStatus
{
    Received = 0,
    Processed = 1,
    Failed = 2,
    Ignored = 3
}

public enum LoginHistoryResult
{
    Succeeded = 0,
    Failed = 1,
    Locked = 2,
    Blocked = 3
}
