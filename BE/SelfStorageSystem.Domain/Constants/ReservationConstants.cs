namespace SelfStorageSystem.Domain.Constants;

public static class ReservationStatusConstants
{
    public const string Pending = "pending";
    public const string AwaitingDeposit = "awaiting_deposit";
    public const string Confirmed = "confirmed";
    public const string Converted = "converted";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}

public static class AllocationStatusConstants
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Completed = "completed";
}

public static class AllocationKindConstants
{
    public const string Reservation = "reservation_hold";
    public const string ReservationHold = "reservation_hold";
    public const string Agreement = "rental";
    public const string Rental = "rental";
    public const string Transfer = "transfer";
}

public static class StorageUnitStatusConstants
{
    public const string Available = "available";
    public const string Reserved = "reserved";
    public const string Occupied = "occupied";
    public const string UnderMaintenance = "under_maintenance";
    public const string InUse = "in_use";
}
