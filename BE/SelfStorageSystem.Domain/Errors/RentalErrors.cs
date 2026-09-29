using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class RentalErrors
{
    public static readonly Error CustomerNotFound =
        Error.NotFound("Rental.CustomerNotFound", "Customer profile was not found.");

    public static readonly Error AgreementNotFound =
        Error.NotFound("Rental.AgreementNotFound", "Rental agreement was not found or does not belong to you.");

    public static readonly Error AgreementNotCheckedIn =
        Error.Validation("Rental.NotCheckedIn", "Access credentials are only issued after check-in has been completed (BR-RSV-04).");

    public static readonly Error AccessSuspendedDueToOverdue =
        Error.Conflict("Rental.AccessSuspended", "Access is currently suspended due to overdue payment exceeding 1 day (BR-REN-03). Please settle your balance.");

    public static readonly Error InvalidPinFormat =
        Error.Validation("Rental.InvalidPinFormat", "PIN must be exactly 6 numeric digits.");

    public static readonly Error PinTooSimple =
        Error.Validation("Rental.PinTooSimple", "PIN cannot be repeated or consecutive numbers (e.g. 111111, 123456, 654321).");

    public static readonly Error IncorrectCurrentPin =
        Error.Validation("Rental.IncorrectCurrentPin", "The current PIN entered is incorrect.");

    public static readonly Error HandoverNotFound =
        Error.NotFound("Rental.HandoverNotFound", "Handover record was not found for this rental agreement.");
}
