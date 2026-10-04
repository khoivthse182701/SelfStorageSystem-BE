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
    public const string QrCode = "qr_code";
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
    public const string Inspected = "inspected";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class RenewalStatusConstants
{
    public const string Requested = "requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";
}

public static class TransferRequestStatusConstants
{
    public const string Requested = "requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public static class AuthorizedMemberStatusConstants
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}

public static class InspectionStatusConstants
{
    public const string Passed = "passed";
    public const string Completed = "completed";
    public const string Failed = "failed";
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
    public const string DefaultCheckInCondition = "Good / Ready for Move-In";
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
}
