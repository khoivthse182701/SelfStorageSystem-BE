namespace SelfStorageSystem.Domain.Constants;

public static class RentalAgreementStatusConstants
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string ExpiringSoon = "expiring_soon";
    public const string Terminated = "terminated";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class CredentialTypeConstants
{
    public const string Pin = "pin";
    public const string Card = "card";
    public const string Qr = "qr";
    public const string QrCode = "qr";
    public const string Key = "key";
    public const string Mobile = "mobile";
}

public static class CredentialStatusConstants
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Revoked = "revoked";
    public const string Pending = "pending";
}

public static class HandoverTypeConstants
{
    public const string CheckIn = "check_in";
    public const string CheckOut = "check_out";
    public const string Transfer = "transfer";
}

public static class MoveOutStatusConstants
{
    public const string Requested = "requested";
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string InspectionPending = "inspection_pending";
    public const string SettlementPending = "settlement_pending";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public const string Inspected = "completed";
}

public static class RenewalStatusConstants
{
    public const string PendingPayment = "pending_payment";
    public const string Paid = "paid";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";

    public const string Requested = "pending_payment";
}

public static class TransferRequestStatusConstants
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Scheduled = "scheduled";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";

    public const string Requested = "pending";
}

public static class AuthorizedMemberStatusConstants
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}

public static class InspectionStatusConstants
{
    public const string Draft = "draft";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public const string Passed = "completed";
}

public static class OverallConditionConstants
{
    public const string Good = "good";
    public const string Acceptable = "acceptable";
    public const string Damaged = "damaged";
    public const string Unsafe = "unsafe";

    public static readonly string[] All = { Good, Acceptable, Damaged, Unsafe };
}

public static class InspectionItemConditionConstants
{
    public const string Good = "good";
    public const string Acceptable = "acceptable";
    public const string Damaged = "damaged";
    public const string Missing = "missing";
    public const string NotApplicable = "not_applicable";

    public static readonly string[] All = { Good, Acceptable, Damaged, Missing, NotApplicable };
}

public static class MaintenanceWorkOrderStatusConstants
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class ProposalStatusConstants
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class StaffConstants
{
    public const string AgreementPrefix = "AGR-";
    public const string WorkOrderPrefix = "MWO-";
    public const string DefaultCheckInCondition = OverallConditionConstants.Good;
    public const string DefaultCheckInSummary = "Check-in inspection completed by staff.";
    public const string DefaultMoveOutSummary = "Check-out return inspection performed by staff.";
    public const string DefaultNotApplicable = "N/A";
    public const string DefaultCustomerName = "Customer";
    public const string DefaultCredentialStatusNone = "none";
}

public static class RentalSuspensionReasons
{
    public const string OverdueDebtExceeded = "Access is currently suspended due to overdue payment exceeding 1 day (BR-REN-03). Please settle your overdue invoices.";
}

public static class PinSyncStatusConstants
{
    public const string Synced = "synced";
    public const string Pending = "pending";
    public const string Failed = "failed";
}

public static class RentalPinMessages
{
    public const string PinSyncPending = "PIN updated successfully. Syncing with door smart lock. Please allow up to 1 minute before using the new PIN at the door.";
    public const string PinSyncCompleted = "PIN updated and synchronized successfully.";
}

public static class RentalLogMessages
{
    public const string RentalsRetrieved = "Customer {CustomerId} retrieved {Count} active/expiring rentals.";
    public const string AccessCredentialsRetrieved = "Customer {CustomerId} retrieved access credentials for agreement {AgreementId}. Status: {Status}";
    public const string PinChanged = "Customer {CustomerId} successfully changed PIN for agreement {AgreementId}.";
    public const string HandoverRecordRetrieved = "Customer {CustomerId} retrieved handover record for agreement {AgreementId}.";
    public const string OutsideBusinessHours = "Access credential request rejected for agreement {AgreementId}. Outside business hours ({Opening}-{Closing}). Current facility time: {CurrentTime}";
    public const string PinLockout = "Agreement {AgreementId} locked out from changing PIN due to too many failed attempts.";
    public const string FailedPinVerification = "Failed current PIN verification for agreement {AgreementId}. Attempt {AttemptCount}/{MaxAttempts}";
}

public static class RentalDefaults
{
    public const string DefaultTimezone = "Asia/Ho_Chi_Minh";
    public const string FallbackWindowsTimezone = "SE Asia Standard Time";
    public const string GateAccessClaimType = "GATE_ACCESS";
    public const string PinLockoutCacheKeyPrefix = "pin_change_failed_attempts:";
    public const string DefaultRefundPreviewNote = "Estimated net refund is subject to staff final on-site inspection and deduction of any pending damages/overdue balances per BR-FIN-02.";
}

public static class RentalPolicyConstants
{
    public const int DefaultMaxFailedPinAttempts = 5;
    public const int DefaultPinLockoutDurationMinutes = 15;
    public const int DefaultQrTtlSeconds = 120;
    public const int DefaultClockSkewLeewaySeconds = 30;
    public const int DefaultEstimatedPinSyncSeconds = 60;
    public const int MinRenewalMonths = 1;
    public const int MaxRenewalMonths = 12;
}

public static class RentalClaimTypes
{
    public const string AgreementId = "agreementId";
    public const string CustomerId = "customerId";
    public const string FacilityId = "facilityId";
    public const string UnitId = "unitId";
    public const string AgreementNo = "agreementNo";
    public const string Type = "type";
}

public static class RentalSecurityConstants
{
    public static readonly HashSet<string> WeakPins = new(StringComparer.Ordinal)
    {
        "012345", "123456", "234567", "345678", "456789", "567890",
        "987654", "876543", "765432", "654321", "543210", "098765"
    };
}
