namespace SelfStorageSystem.Domain.Constants;

public static class InvoiceStatusConstants
{
    public const string Draft = "draft";
    public const string Open = "open";
    public const string PartiallyPaid = "partially_paid";
    public const string Paid = "paid";
    public const string Voided = "voided";
    public const string Overdue = "overdue";
}

public static class InvoiceLineTypeConstants
{
    public const string Rent = "rent";
    public const string Deposit = "deposit";
    public const string BookingFee = "booking_fee";
    public const string Discount = "discount";
    public const string Penalty = "penalty";
    public const string Service = "service";
}
