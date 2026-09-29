namespace SelfStorageSystem.Domain.Errors;

using SelfStorageSystem.Domain.Common;

/// <summary>
/// Typed error codes for customer payment operations.
/// </summary>
public static class PaymentErrors
{
    // ── Reservation / Invoice lookup ───────────────────────────────────────
    public static readonly Error ReservationNotFound =
        Error.NotFound("Payment.ReservationNotFound", "Reservation not found for this customer.");

    public static readonly Error InvoiceNotFound =
        Error.NotFound("Payment.InvoiceNotFound", "No unpaid invoice found for this reservation.");

    // ── Business Rules ─────────────────────────────────────────────────────
    public static readonly Error HoldExpired =
        Error.Conflict("Payment.HoldExpired", "Reservation hold time has expired as per BR-RSV-01. Please reserve a new unit.");

    public static readonly Error AlreadyConfirmed =
        Error.Conflict("Payment.AlreadyConfirmed", "This reservation has already been confirmed and paid.");

    public static readonly Error InvoiceAlreadyPaid =
        Error.Conflict("Payment.InvoiceAlreadyPaid", "Invoice has already been fully paid.");
}
