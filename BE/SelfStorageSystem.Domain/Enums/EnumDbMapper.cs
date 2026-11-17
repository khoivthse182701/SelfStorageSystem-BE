namespace SelfStorageSystem.Domain.Enums;

/// <summary>
/// Authoritative 1-to-1 bidirectional mapping between strongly-typed Domain Enums
/// and canonical SQL snake_case column values.
/// </summary>
public static class EnumDbMapper
{
    public static string ToDbValue(this UnitStatus status) => status switch
    {
        UnitStatus.Available => "available",
        UnitStatus.Reserved => "reserved",
        UnitStatus.Occupied => "occupied",
        UnitStatus.PendingInspection => "pending_inspection",
        UnitStatus.Cleaning => "cleaning",
        UnitStatus.Maintenance => "maintenance",
        UnitStatus.Damaged => "damaged",
        UnitStatus.OutOfService => "out_of_service",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this ReservationStatus status) => status switch
    {
        ReservationStatus.Pending => "pending",
        ReservationStatus.AwaitingDeposit => "awaiting_deposit",
        ReservationStatus.Confirmed => "confirmed",
        ReservationStatus.CheckedIn => "checked_in",
        ReservationStatus.Converted => "converted",
        ReservationStatus.Completed => "completed",
        ReservationStatus.NoShow => "no_show",
        ReservationStatus.Cancelled => "cancelled",
        ReservationStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this ContractStatus status) => status switch
    {
        ContractStatus.Draft => "draft",
        ContractStatus.Scheduled => "scheduled",
        ContractStatus.Active => "active",
        ContractStatus.ExpiringSoon => "expiring_soon",
        ContractStatus.Extended => "extended",
        ContractStatus.Overdue => "overdue",
        ContractStatus.Defaulted => "defaulted",
        ContractStatus.MoveOutScheduled => "move_out_scheduled",
        ContractStatus.CheckoutPending => "checkout_pending",
        ContractStatus.Expired => "expired",
        ContractStatus.Terminated => "terminated",
        ContractStatus.Completed => "completed",
        ContractStatus.Closed => "closed",
        ContractStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this PaymentStatus status) => status switch
    {
        PaymentStatus.Initiated => "initiated",
        PaymentStatus.Pending => "pending",
        PaymentStatus.Succeeded => "succeeded",
        PaymentStatus.Failed => "failed",
        PaymentStatus.Cancelled => "cancelled",
        PaymentStatus.PartiallyRefunded => "partially_refunded",
        PaymentStatus.Refunded => "refunded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this TicketStatus status) => status switch
    {
        TicketStatus.Open => "open",
        TicketStatus.PendingManagerReview => "pending_manager_review",
        TicketStatus.Assigned => "assigned",
        TicketStatus.InProgress => "in_progress",
        TicketStatus.WaitingForCustomer => "waiting_for_customer",
        TicketStatus.WaitingForMaintenance => "waiting_for_maintenance",
        TicketStatus.SevereNeedRelocation => "severe_need_relocation",
        TicketStatus.Assessed => "assessed",
        TicketStatus.Resolved => "resolved",
        TicketStatus.Closed => "closed",
        TicketStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this UserRoleCode role) => role switch
    {
        UserRoleCode.Customer => "storage_customer",
        UserRoleCode.Staff => "facility_staff",
        UserRoleCode.Manager => "facility_manager",
        UserRoleCode.Admin => "system_administrator",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static string ToApiRoleName(this UserRoleCode role) => role switch
    {
        UserRoleCode.Customer => "Customer",
        UserRoleCode.Staff => "Staff",
        UserRoleCode.Manager => "Manager",
        UserRoleCode.Admin => "Admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static string ToDbValue(this UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Locked => "locked",
        UserStatus.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this EmploymentStatus status) => status switch
    {
        EmploymentStatus.Active => "active",
        EmploymentStatus.Leave => "leave",
        EmploymentStatus.Terminated => "terminated",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this FacilityStatus status) => status switch
    {
        FacilityStatus.Draft => "draft",
        FacilityStatus.Active => "active",
        FacilityStatus.Inactive => "inactive",
        FacilityStatus.Maintenance => "maintenance",
        FacilityStatus.SuspendedNewBookings => "suspended_new_bookings",
        FacilityStatus.TemporarilyClosed => "temporarily_closed",
        FacilityStatus.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this AreaType value) => value switch
    {
        AreaType.Building => "building",
        AreaType.Floor => "floor",
        AreaType.Zone => "zone",
        AreaType.Section => "section",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "draft",
        InvoiceStatus.Open => "open",
        InvoiceStatus.PartiallyPaid => "partially_paid",
        InvoiceStatus.Paid => "paid",
        InvoiceStatus.Overdue => "overdue",
        InvoiceStatus.Voided => "voided",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this InvoiceLineType value) => value switch
    {
        InvoiceLineType.Deposit => "deposit",
        InvoiceLineType.Rent => "rent",
        InvoiceLineType.Renewal => "renewal",
        InvoiceLineType.LateFee => "late_fee",
        InvoiceLineType.Damage => "damage",
        InvoiceLineType.Maintenance => "maintenance",
        InvoiceLineType.BookingFee => "booking_fee",
        InvoiceLineType.Discount => "discount",
        InvoiceLineType.Penalty => "penalty",
        InvoiceLineType.Service => "service",
        InvoiceLineType.Fee => "fee",
        InvoiceLineType.Tax => "tax",
        InvoiceLineType.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PaymentMethod value) => value switch
    {
        PaymentMethod.Cash => "cash",
        PaymentMethod.BankTransfer => "bank_transfer",
        PaymentMethod.CreditCard => "credit_card",
        PaymentMethod.VnPay => "vnpay",
        PaymentMethod.PayOs => "payos",
        PaymentMethod.Stripe => "stripe",
        PaymentMethod.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this RefundStatus status) => status switch
    {
        RefundStatus.Requested => "requested",
        RefundStatus.Pending => "pending",
        RefundStatus.Approved => "approved",
        RefundStatus.Processing => "processing",
        RefundStatus.Succeeded => "succeeded",
        RefundStatus.Completed => "completed",
        RefundStatus.Failed => "failed",
        RefundStatus.Rejected => "rejected",
        RefundStatus.PendingManagerPayout => "pending_manager_payout",
        RefundStatus.PendingCounterPayout => "pending_counter_payout",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ToDbValue(this ApprovalDecision value) => value switch
    {
        ApprovalDecision.Approved => "approved",
        ApprovalDecision.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this FeeType value) => value switch
    {
        FeeType.LateFee => "late_fee",
        FeeType.DamageFee => "damage_fee",
        FeeType.LostKeyFee => "lost_key_fee",
        FeeType.MaintenanceFee => "maintenance_fee",
        FeeType.Booking => "booking",
        FeeType.BookingFee => "booking_fee",
        FeeType.Tax => "tax",
        FeeType.VatTax => "vat_tax",
        FeeType.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this FeeCalculationMethod value) => value switch
    {
        FeeCalculationMethod.Flat => "flat",
        FeeCalculationMethod.Percentage => "percentage",
        FeeCalculationMethod.PerDay => "per_day",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PromotionDiscountType value) => value switch
    {
        PromotionDiscountType.Fixed => "fixed",
        PromotionDiscountType.Percentage => "percentage",
        PromotionDiscountType.FreeDays => "free_days",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PromotionRuleType value) => value switch
    {
        PromotionRuleType.MinimumMonths => "minimum_months",
        PromotionRuleType.Month => "month",
        PromotionRuleType.MinimumAmount => "minimum_amount",
        PromotionRuleType.Facility => "facility",
        PromotionRuleType.UnitType => "unit_type",
        PromotionRuleType.NewCustomer => "new_customer",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PromotionOperator value) => value switch
    {
        PromotionOperator.Eq => "eq",
        PromotionOperator.Gte => "gte",
        PromotionOperator.Lte => "lte",
        PromotionOperator.In => "in",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PromotionRedemptionStatus value) => value switch
    {
        PromotionRedemptionStatus.Reserved => "reserved",
        PromotionRedemptionStatus.Applied => "applied",
        PromotionRedemptionStatus.Released => "released",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this PolicyType value) => value switch
    {
        PolicyType.RentalTerms => "rental_terms",
        PolicyType.Deposit => "deposit",
        PolicyType.Cancellation => "cancellation",
        PolicyType.Renewal => "renewal",
        PolicyType.MoveOut => "move_out",
        PolicyType.Overdue => "overdue",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AllocationKind value) => value switch
    {
        AllocationKind.ReservationHold => "reservation_hold",
        AllocationKind.Rental => "rental",
        AllocationKind.Transfer => "transfer",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AllocationStatus value) => value switch
    {
        AllocationStatus.Active => "active",
        AllocationStatus.Consumed => "consumed",
        AllocationStatus.Released => "released",
        AllocationStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this RenewalStatus value) => value switch
    {
        RenewalStatus.PendingPayment => "pending_payment",
        RenewalStatus.Paid => "paid",
        RenewalStatus.Approved => "approved",
        RenewalStatus.Rejected => "rejected",
        RenewalStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this TransferRequestStatus value) => value switch
    {
        TransferRequestStatus.PendingPayment => "pending_payment",
        TransferRequestStatus.Paid => "paid",
        TransferRequestStatus.MovedOutInTransit => "moved_out_in_transit",
        TransferRequestStatus.CancelledOverdueMoveOut => "cancelled_overdue_move_out",
        TransferRequestStatus.Pending => "pending",
        TransferRequestStatus.Approved => "approved",
        TransferRequestStatus.Scheduled => "scheduled",
        TransferRequestStatus.Completed => "completed",
        TransferRequestStatus.Rejected => "rejected",
        TransferRequestStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this MoveOutStatus value) => value switch
    {
        MoveOutStatus.Requested => "requested",
        MoveOutStatus.Scheduled => "scheduled",
        MoveOutStatus.InProgress => "in_progress",
        MoveOutStatus.InspectionPending => "inspection_pending",
        MoveOutStatus.SettlementPending => "settlement_pending",
        MoveOutStatus.Completed => "completed",
        MoveOutStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AuthorizedMemberStatus value) => value switch
    {
        AuthorizedMemberStatus.Pending => "pending",
        AuthorizedMemberStatus.Active => "active",
        AuthorizedMemberStatus.Revoked => "revoked",
        AuthorizedMemberStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this CredentialType value) => value switch
    {
        CredentialType.Pin => "pin",
        CredentialType.Card => "card",
        CredentialType.Qr => "qr",
        CredentialType.Key => "key",
        CredentialType.Mobile => "mobile",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this CredentialStatus value) => value switch
    {
        CredentialStatus.Pending => "pending",
        CredentialStatus.Active => "active",
        CredentialStatus.Suspended => "suspended",
        CredentialStatus.Revoked => "revoked",
        CredentialStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AccessPointType value) => value switch
    {
        AccessPointType.Gate => "gate",
        AccessPointType.BuildingDoor => "building_door",
        AccessPointType.FloorDoor => "floor_door",
        AccessPointType.UnitDoor => "unit_door",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AccessPointStatus value) => value switch
    {
        AccessPointStatus.Active => "active",
        AccessPointStatus.Offline => "offline",
        AccessPointStatus.Maintenance => "maintenance",
        AccessPointStatus.Retired => "retired",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this AccessEventResult value) => value switch
    {
        AccessEventResult.Granted => "granted",
        AccessEventResult.Denied => "denied",
        AccessEventResult.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this DelinquencyCaseStatus value) => value switch
    {
        DelinquencyCaseStatus.Grace => "grace",
        DelinquencyCaseStatus.Delinquent => "delinquent",
        DelinquencyCaseStatus.Cured => "cured",
        DelinquencyCaseStatus.Waived => "waived",
        DelinquencyCaseStatus.TerminationStarted => "termination_started",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this DelinquencyActionType value) => value switch
    {
        DelinquencyActionType.ReminderSent => "reminder_sent",
        DelinquencyActionType.LateFeeAdded => "late_fee_added",
        DelinquencyActionType.CredentialSuspended => "credential_suspended",
        DelinquencyActionType.CustomerContacted => "customer_contacted",
        DelinquencyActionType.PaymentPlan => "payment_plan",
        DelinquencyActionType.Waived => "waived",
        DelinquencyActionType.TerminationStarted => "termination_started",
        DelinquencyActionType.Note => "note",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this TicketCategory value) => value switch
    {
        TicketCategory.Unit => "unit",
        TicketCategory.Access => "access",
        TicketCategory.Payment => "payment",
        TicketCategory.StoredItem => "stored_item",
        TicketCategory.Maintenance => "maintenance",
        TicketCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this TicketPriority value) => value switch
    {
        TicketPriority.Low => "low",
        TicketPriority.Normal => "normal",
        TicketPriority.High => "high",
        TicketPriority.Urgent => "urgent",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this ProposalStatus value) => value switch
    {
        ProposalStatus.Pending => "pending",
        ProposalStatus.Approved => "approved",
        ProposalStatus.Rejected => "rejected",
        ProposalStatus.Invoiced => "invoiced",
        ProposalStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this InspectionType value) => value switch
    {
        InspectionType.CheckIn => "check_in",
        InspectionType.CheckOut => "check_out",
        InspectionType.Transfer => "transfer",
        InspectionType.Security => "security",
        InspectionType.FireSafety => "fire_safety",
        InspectionType.Routine => "routine",
        InspectionType.Incident => "incident",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this InspectionStatus value) => value switch
    {
        InspectionStatus.Draft => "draft",
        InspectionStatus.Completed => "completed",
        InspectionStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this OverallCondition value) => value switch
    {
        OverallCondition.Good => "good",
        OverallCondition.Acceptable => "acceptable",
        OverallCondition.Damaged => "damaged",
        OverallCondition.Unsafe => "unsafe",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this InspectionItemCondition value) => value switch
    {
        InspectionItemCondition.Good => "good",
        InspectionItemCondition.Acceptable => "acceptable",
        InspectionItemCondition.Damaged => "damaged",
        InspectionItemCondition.Missing => "missing",
        InspectionItemCondition.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this HandoverType value) => value switch
    {
        HandoverType.CheckIn => "check_in",
        HandoverType.CheckOut => "check_out",
        HandoverType.Transfer => "transfer",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this IdentityVerificationMethod value) => value switch
    {
        IdentityVerificationMethod.GovernmentId => "government_id",
        IdentityVerificationMethod.Passport => "passport",
        IdentityVerificationMethod.DigitalIdentity => "digital_identity",
        IdentityVerificationMethod.Manual => "manual",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this IdentityVerificationResult value) => value switch
    {
        IdentityVerificationResult.Verified => "verified",
        IdentityVerificationResult.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this MaintenanceWorkOrderStatus value) => value switch
    {
        MaintenanceWorkOrderStatus.Open => "open",
        MaintenanceWorkOrderStatus.Assigned => "assigned",
        MaintenanceWorkOrderStatus.InProgress => "in_progress",
        MaintenanceWorkOrderStatus.OnHold => "on_hold",
        MaintenanceWorkOrderStatus.AwaitingVerification => "awaiting_verification",
        MaintenanceWorkOrderStatus.Completed => "completed",
        MaintenanceWorkOrderStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this StaffShiftStatus value) => value switch
    {
        StaffShiftStatus.Planned => "planned",
        StaffShiftStatus.Open => "open",
        StaffShiftStatus.Completed => "completed",
        StaffShiftStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this DutyRole value) => value switch
    {
        DutyRole.Staff => "staff",
        DutyRole.ShiftLead => "shift_lead",
        DutyRole.ManagerOnCall => "manager_on_call",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this ShiftAssignmentStatus value) => value switch
    {
        ShiftAssignmentStatus.Scheduled => "scheduled",
        ShiftAssignmentStatus.CheckedIn => "checked_in",
        ShiftAssignmentStatus.CheckedOut => "checked_out",
        ShiftAssignmentStatus.Absent => "absent",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this StaffTaskType value) => value switch
    {
        StaffTaskType.CheckIn => "check_in",
        StaffTaskType.CheckOut => "check_out",
        StaffTaskType.Inspection => "inspection",
        StaffTaskType.Maintenance => "maintenance",
        StaffTaskType.Support => "support",
        StaffTaskType.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this StaffTaskStatus value) => value switch
    {
        StaffTaskStatus.Todo => "todo",
        StaffTaskStatus.InProgress => "in_progress",
        StaffTaskStatus.Blocked => "blocked",
        StaffTaskStatus.Done => "done",
        StaffTaskStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this NotificationChannel value) => value switch
    {
        NotificationChannel.InApp => "in_app",
        NotificationChannel.Email => "email",
        NotificationChannel.Sms => "sms",
        NotificationChannel.Push => "push",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this NotificationStatus value) => value switch
    {
        NotificationStatus.Pending => "pending",
        NotificationStatus.Processing => "processing",
        NotificationStatus.Sent => "sent",
        NotificationStatus.Failed => "failed",
        NotificationStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this IntegrationEventStatus value) => value switch
    {
        IntegrationEventStatus.Received => "received",
        IntegrationEventStatus.Processed => "processed",
        IntegrationEventStatus.Failed => "failed",
        IntegrationEventStatus.Ignored => "ignored",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDbValue(this LoginHistoryResult value) => value switch
    {
        LoginHistoryResult.Succeeded => "succeeded",
        LoginHistoryResult.Failed => "failed",
        LoginHistoryResult.Locked => "locked",
        LoginHistoryResult.Blocked => "blocked",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static UnitStatus ParseUnitStatus(string? value) =>
        TryParseUnitStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown UnitStatus '{value}'.", nameof(value));

    public static bool TryParseUnitStatus(string? value, out UnitStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "available": status = UnitStatus.Available; return true;
            case "reserved": status = UnitStatus.Reserved; return true;
            case "occupied":
            case "in_use": status = UnitStatus.Occupied; return true;
            case "pending_inspection": status = UnitStatus.PendingInspection; return true;
            case "cleaning": status = UnitStatus.Cleaning; return true;
            case "maintenance":
            case "under_maintenance": status = UnitStatus.Maintenance; return true;
            case "damaged": status = UnitStatus.Damaged; return true;
            case "out_of_service": status = UnitStatus.OutOfService; return true;
            default: status = default; return false;
        }
    }

    public static ReservationStatus ParseReservationStatus(string? value) =>
        TryParseReservationStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown ReservationStatus '{value}'.", nameof(value));

    public static bool TryParseReservationStatus(string? value, out ReservationStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "pending": status = ReservationStatus.Pending; return true;
            case "awaiting_deposit": status = ReservationStatus.AwaitingDeposit; return true;
            case "confirmed": status = ReservationStatus.Confirmed; return true;
            case "checked_in": status = ReservationStatus.CheckedIn; return true;
            case "converted": status = ReservationStatus.Converted; return true;
            case "completed": status = ReservationStatus.Completed; return true;
            case "no_show": status = ReservationStatus.NoShow; return true;
            case "cancelled": status = ReservationStatus.Cancelled; return true;
            case "expired": status = ReservationStatus.Expired; return true;
            default: status = default; return false;
        }
    }

    public static ContractStatus ParseContractStatus(string? value) =>
        TryParseContractStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown ContractStatus '{value}'.", nameof(value));

    public static bool TryParseContractStatus(string? value, out ContractStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "draft": status = ContractStatus.Draft; return true;
            case "scheduled": status = ContractStatus.Scheduled; return true;
            case "active": status = ContractStatus.Active; return true;
            case "expiring_soon": status = ContractStatus.ExpiringSoon; return true;
            case "extended": status = ContractStatus.Extended; return true;
            case "overdue": status = ContractStatus.Overdue; return true;
            case "defaulted": status = ContractStatus.Defaulted; return true;
            case "move_out_scheduled": status = ContractStatus.MoveOutScheduled; return true;
            case "checkout_pending": status = ContractStatus.CheckoutPending; return true;
            case "expired": status = ContractStatus.Expired; return true;
            case "terminated": status = ContractStatus.Terminated; return true;
            case "completed": status = ContractStatus.Completed; return true;
            case "closed": status = ContractStatus.Closed; return true;
            case "cancelled": status = ContractStatus.Cancelled; return true;
            default: status = default; return false;
        }
    }

    public static PaymentStatus ParsePaymentStatus(string? value) =>
        TryParsePaymentStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown PaymentStatus '{value}'.", nameof(value));

    public static bool TryParsePaymentStatus(string? value, out PaymentStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "initiated": status = PaymentStatus.Initiated; return true;
            case "pending": status = PaymentStatus.Pending; return true;
            case "succeeded": status = PaymentStatus.Succeeded; return true;
            case "failed": status = PaymentStatus.Failed; return true;
            case "cancelled": status = PaymentStatus.Cancelled; return true;
            case "partially_refunded": status = PaymentStatus.PartiallyRefunded; return true;
            case "refunded": status = PaymentStatus.Refunded; return true;
            default: status = default; return false;
        }
    }

    public static TicketStatus ParseTicketStatus(string? value) =>
        TryParseTicketStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown TicketStatus '{value}'.", nameof(value));

    public static bool TryParseTicketStatus(string? value, out TicketStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "open": status = TicketStatus.Open; return true;
            case "pending_manager_review": status = TicketStatus.PendingManagerReview; return true;
            case "assigned": status = TicketStatus.Assigned; return true;
            case "in_progress": status = TicketStatus.InProgress; return true;
            case "waiting_for_customer": status = TicketStatus.WaitingForCustomer; return true;
            case "waiting_for_maintenance": status = TicketStatus.WaitingForMaintenance; return true;
            case "severe_need_relocation": status = TicketStatus.SevereNeedRelocation; return true;
            case "assessed": status = TicketStatus.Assessed; return true;
            case "resolved": status = TicketStatus.Resolved; return true;
            case "closed": status = TicketStatus.Closed; return true;
            case "cancelled": status = TicketStatus.Cancelled; return true;
            default: status = default; return false;
        }
    }

    public static InvoiceStatus ParseInvoiceStatus(string? value) =>
        TryParseInvoiceStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown InvoiceStatus '{value}'.", nameof(value));

    public static bool TryParseInvoiceStatus(string? value, out InvoiceStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "draft": status = InvoiceStatus.Draft; return true;
            case "open": status = InvoiceStatus.Open; return true;
            case "partially_paid": status = InvoiceStatus.PartiallyPaid; return true;
            case "paid": status = InvoiceStatus.Paid; return true;
            case "overdue": status = InvoiceStatus.Overdue; return true;
            case "voided": status = InvoiceStatus.Voided; return true;
            default: status = default; return false;
        }
    }

    public static RefundStatus ParseRefundStatus(string? value) =>
        TryParseRefundStatus(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unknown RefundStatus '{value}'.", nameof(value));

    public static bool TryParseRefundStatus(string? value, out RefundStatus status)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "requested": status = RefundStatus.Requested; return true;
            case "pending": status = RefundStatus.Pending; return true;
            case "approved": status = RefundStatus.Approved; return true;
            case "processing": status = RefundStatus.Processing; return true;
            case "succeeded": status = RefundStatus.Succeeded; return true;
            case "completed": status = RefundStatus.Completed; return true;
            case "failed": status = RefundStatus.Failed; return true;
            case "rejected": status = RefundStatus.Rejected; return true;
            case "pending_manager_payout": status = RefundStatus.PendingManagerPayout; return true;
            case "pending_counter_payout": status = RefundStatus.PendingCounterPayout; return true;
            default: status = default; return false;
        }
    }

    public static bool TryParseRoleCode(string? value, out UserRoleCode role)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "storage_customer":
            case "customer":
                role = UserRoleCode.Customer;
                return true;
            case "facility_staff":
            case "staff":
                role = UserRoleCode.Staff;
                return true;
            case "facility_manager":
            case "manager":
            case "business_operations_manager":
                role = UserRoleCode.Manager;
                return true;
            case "system_administrator":
            case "admin":
            case "system_admin":
                role = UserRoleCode.Admin;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static IReadOnlyList<string> GetAllDbValues<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => ToDbValueDynamic(v)).Distinct(StringComparer.Ordinal).ToArray();

    public static string ToSqlCheckInClause<TEnum>(string columnName) where TEnum : struct, Enum
    {
        var values = GetAllDbValues<TEnum>().Select(v => $"'{v}'");
        return $"[{columnName}] IN ({string.Join(", ", values)})";
    }

    private static string ToDbValueDynamic<TEnum>(TEnum value) where TEnum : struct, Enum => value switch
    {
        UnitStatus u => u.ToDbValue(),
        ReservationStatus r => r.ToDbValue(),
        ContractStatus c => c.ToDbValue(),
        PaymentStatus p => p.ToDbValue(),
        TicketStatus t => t.ToDbValue(),
        UserRoleCode ur => ur.ToDbValue(),
        UserStatus us => us.ToDbValue(),
        EmploymentStatus es => es.ToDbValue(),
        FacilityStatus fs => fs.ToDbValue(),
        AreaType at => at.ToDbValue(),
        InvoiceStatus ins => ins.ToDbValue(),
        InvoiceLineType ilt => ilt.ToDbValue(),
        PaymentMethod pm => pm.ToDbValue(),
        RefundStatus rs => rs.ToDbValue(),
        ApprovalDecision ad => ad.ToDbValue(),
        FeeType ft => ft.ToDbValue(),
        FeeCalculationMethod fcm => fcm.ToDbValue(),
        PromotionDiscountType pdt => pdt.ToDbValue(),
        PromotionRuleType prt => prt.ToDbValue(),
        PromotionOperator po => po.ToDbValue(),
        PromotionRedemptionStatus prs => prs.ToDbValue(),
        PolicyType pt => pt.ToDbValue(),
        AllocationKind ak => ak.ToDbValue(),
        AllocationStatus als => als.ToDbValue(),
        RenewalStatus rns => rns.ToDbValue(),
        TransferRequestStatus trs => trs.ToDbValue(),
        MoveOutStatus mos => mos.ToDbValue(),
        AuthorizedMemberStatus ams => ams.ToDbValue(),
        CredentialType ct => ct.ToDbValue(),
        CredentialStatus cs => cs.ToDbValue(),
        AccessPointType apt => apt.ToDbValue(),
        AccessPointStatus aps => aps.ToDbValue(),
        AccessEventResult aer => aer.ToDbValue(),
        DelinquencyCaseStatus dcs => dcs.ToDbValue(),
        DelinquencyActionType dat => dat.ToDbValue(),
        TicketCategory tc => tc.ToDbValue(),
        TicketPriority tp => tp.ToDbValue(),
        ProposalStatus ps => ps.ToDbValue(),
        InspectionType it => it.ToDbValue(),
        InspectionStatus ist => ist.ToDbValue(),
        OverallCondition oc => oc.ToDbValue(),
        InspectionItemCondition iic => iic.ToDbValue(),
        HandoverType ht => ht.ToDbValue(),
        IdentityVerificationMethod ivm => ivm.ToDbValue(),
        IdentityVerificationResult ivr => ivr.ToDbValue(),
        MaintenanceWorkOrderStatus mwo => mwo.ToDbValue(),
        StaffShiftStatus sss => sss.ToDbValue(),
        DutyRole dr => dr.ToDbValue(),
        ShiftAssignmentStatus sas => sas.ToDbValue(),
        StaffTaskType stt => stt.ToDbValue(),
        StaffTaskStatus sts => sts.ToDbValue(),
        NotificationChannel nc => nc.ToDbValue(),
        NotificationStatus ns => ns.ToDbValue(),
        IntegrationEventStatus ies => ies.ToDbValue(),
        LoginHistoryResult lhr => lhr.ToDbValue(),
        _ => throw new NotSupportedException($"Enum {typeof(TEnum).Name} does not have a DB mapping.")
    };
}
