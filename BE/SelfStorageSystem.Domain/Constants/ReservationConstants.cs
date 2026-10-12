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
    public const string Consumed = "consumed";
    public const string Released = "released";
    public const string Cancelled = "released";
    public const string Expired = "expired";
    public const string Completed = "released";
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
    public const string PendingInspection = "pending_inspection";
    public const string Maintenance = "maintenance";
    public const string UnderMaintenance = "maintenance";
    public const string OutOfService = "out_of_service";
    public const string InUse = "occupied";
}

