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

    public static readonly Error OutsideBusinessHours =
        Error.Conflict("Rental.OutsideBusinessHours", "Access is not available outside of facility business hours.");

    public static readonly Error TooManyFailedPinAttempts =
        Error.Conflict("Rental.TooManyFailedPinAttempts", "Too many incorrect PIN attempts. PIN change is temporarily locked for 15 minutes.");

    public static readonly Error MoveOutAlreadyRequested =
        Error.Conflict("Rental.MoveOutAlreadyRequested", "A move-out request is already pending or processed for this rental agreement.");

    public static readonly Error InvalidMoveOutDate =
        Error.Validation("Rental.InvalidMoveOutDate", "Requested move-out date cannot be earlier than today.");

    public static readonly Error MoveOutNotFound =
        Error.NotFound("Rental.MoveOutNotFound", "Move-out request was not found.");

    public static readonly Error InvalidRenewalMonths =
        Error.Validation("Rental.InvalidRenewalMonths", "Renewal months must be between 1 and 12.");

    public static readonly Error RenewalAlreadyPending =
        Error.Conflict("Rental.RenewalAlreadyPending", "A renewal request is already pending for this rental agreement.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Rental.MemberNotFound", "Authorized access member was not found or has been revoked.");

    public static readonly Error EmptyMemberName =
        Error.Validation("Rental.EmptyMemberName", "Member full name is required.");

    public static readonly Error TransferRequestAlreadyPending =
        Error.Conflict("Rental.TransferAlreadyPending", "A unit transfer request is already pending for this rental agreement.");

    public static readonly Error TargetUnitTypeNotFound =
        Error.NotFound("Rental.TargetUnitTypeNotFound", "Requested unit type was not found.");

    public static readonly Error EmptyTransferReason =
        Error.Validation("Rental.EmptyTransferReason", "Transfer reason is required.");
}
