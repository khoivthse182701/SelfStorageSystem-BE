namespace SelfStorageSystem.Domain.Constants;

public static class TicketConstants
{
    public const string TicketPrefix = "TCK-";
    public const int RandomSuffixLength = 6;
    public const string DefaultFacilityName = "Unknown";
    public const string DefaultCustomerResolution = "Confirmed and resolved by customer.";
    public const string CustomerSupportTicketsRoute = "api/customer/support-tickets";

    public static string GenerateTicketNo()
    {
        var dateStr = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var randomStr = Guid.NewGuid().ToString()[..RandomSuffixLength].ToUpper();
        return $"{TicketPrefix}{dateStr}-{randomStr}";
    }
}

public static class TicketRatingConstants
{
    public const int MinScore = 1;
    public const int MaxScore = 5;
}

public static class TicketCategoryConstants
{
    public const string Unit = "unit";
    public const string Access = "access";
    public const string Payment = "payment";
    public const string StoredItem = "stored_item";
    public const string Maintenance = "maintenance";
    public const string Other = "other";

    public static readonly string[] All = { Unit, Access, Payment, StoredItem, Maintenance, Other };
}

public static class TicketPriorityConstants
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";

    public static readonly string[] All = { Low, Normal, High, Urgent };
}

public static class TicketStatusConstants
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string WaitingForCustomer = "waiting_for_customer";
    public const string WaitingForMaintenance = "waiting_for_maintenance";
    public const string Assessed = "assessed";
    public const string Resolved = "resolved";
    public const string Closed = "closed";
    public const string Cancelled = "cancelled";
}

public static class TicketDisplayStatusConstants
{
    public const string Reported = "Reported";
    public const string Investigating = "Investigating";
    public const string WaitingForCustomer = "Waiting for customer";
    public const string WaitingForMaintenance = "Waiting for maintenance";
    public const string Assessed = "Assessed";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

