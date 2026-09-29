namespace SelfStorageSystem.Domain.Constants;

public static class FacilityStatusConstants
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Maintenance = "maintenance";
}

public static class AuthConstants
{
    public const string BearerTokenType = "Bearer";
    public const int DefaultAccessTokenMinutes = 60;
}

public static class DbSequenceConstants
{
    public const string ReservationCodeSeqQuery = "SELECT NEXT VALUE FOR core.reservation_code_seq";
    public const string InvoiceNoSeqQuery = "SELECT NEXT VALUE FOR core.invoice_no_seq";
}

public static class ReservationDisplayStatusConstants
{
    public const string Confirmed = "Confirmed";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string PendingPayment = "Pending Payment";
}

public static class DefaultMessageConstants
{
    public const string CheckInInstructions = "Please present your national ID/Passport and your reservation code upon check-in at the facility.";
    public const string DefaultCancellationReason = "Cancelled by customer";
}

public static class TimezoneConstants
{
    public const string DefaultVietnam = "Asia/Ho_Chi_Minh";
    public const string WindowsVietnam = "SE Asia Standard Time";
    public const int VietnamUtcOffsetHours = 7;
}

public static class FeeRuleTypeConstants
{
    public const string Booking = "booking";
    public const string Tax = "tax";
}
