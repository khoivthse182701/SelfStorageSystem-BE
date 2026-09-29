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

public static class RentalSuspensionReasons
{
    public const string OverdueDebtExceeded = "Access is currently suspended due to overdue payment exceeding 1 day (BR-REN-03). Please settle your overdue invoices.";
}

public static class RentalLogMessages
{
    public const string RentalsRetrieved = "Customer {CustomerId} retrieved {Count} active/expiring rentals.";
    public const string AccessCredentialsRetrieved = "Customer {CustomerId} retrieved access credentials for agreement {AgreementId}. Status: {Status}";
    public const string PinChanged = "Customer {CustomerId} successfully changed PIN for agreement {AgreementId}.";
    public const string HandoverRecordRetrieved = "Customer {CustomerId} retrieved handover record for agreement {AgreementId}.";
}
