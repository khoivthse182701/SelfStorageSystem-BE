namespace SelfStorageSystem.Domain.Errors;

using SelfStorageSystem.Domain.Common;

/// <summary>
/// Typed error codes for customer reservation operations.
/// </summary>
public static class ReservationErrors
{
    // ── Customer / Profile ─────────────────────────────────────────────────
    public static readonly Error CustomerNotFound =
        Error.NotFound("Reservation.CustomerNotFound", "Customer profile not found.");

    // ── Lookup ─────────────────────────────────────────────────────────────
    public static readonly Error NotFound =
        Error.NotFound("Reservation.NotFound", "Reservation not found.");

    public static readonly Error FacilityNotFound =
        Error.NotFound("Reservation.FacilityNotFound", "Facility not found or currently inactive.");

    public static readonly Error UnitTypeNotFound =
        Error.NotFound("Reservation.UnitTypeNotFound", "Unit type not found or no longer offered.");

    public static readonly Error StorageUnitNotFound =
        Error.NotFound("Reservation.StorageUnitNotFound", "Selected storage unit not found.");

    // ── Authorization ──────────────────────────────────────────────────────
    public static readonly Error Unauthorized =
        Error.Forbidden("Reservation.Unauthorized", "You do not have permission to access this reservation.");

    public static readonly Error UnauthorizedCancel =
        Error.Forbidden("Reservation.UnauthorizedCancel", "You do not have permission to cancel this reservation.");

    // ── Business Rules ─────────────────────────────────────────────────────
    public static Error InvalidDuration(int min, int max) =>
        Error.Validation("Reservation.InvalidDuration",
            $"Rental duration must be between {min} and {max} months as per BR-RSV-02.");

    public static readonly Error StartDateInPast =
        Error.Validation("Reservation.StartDateInPast", "Rental start date cannot be in the past.");

    public static readonly Error NoPublishedRate =
        Error.Failure("Reservation.NoPublishedRate", "No published price rate found for this unit type at the selected facility.");

    public static readonly Error UnitIncompatible =
        Error.Validation("Reservation.UnitIncompatible", "Selected unit is incompatible with chosen facility or unit type.");

    public static readonly Error UnitNotAvailable =
        Error.Conflict("Reservation.UnitNotAvailable", "This storage unit has already been reserved or occupied. Please select another unit on the floor map.");

    public static readonly Error NoAvailableUnits =
        Error.Conflict("Reservation.NoAvailableUnits", "No available storage units of this type currently exist at the selected facility.");

    public static readonly Error CannotCancelNonPending =
        Error.Conflict("Reservation.CannotCancelNonPending", "Only pending unpaid reservations can be cancelled.");

    // ── Promotion ──────────────────────────────────────────────────────────
    public static readonly Error PromotionInvalidOrExpired =
        Error.Validation("Reservation.PromotionInvalidOrExpired", "Promotion code is invalid or has expired.");

    public static readonly Error PromotionUsageLimitReached =
        Error.Conflict("Reservation.PromotionUsageLimitReached", "Promotion code has reached its overall usage limit.");

    public static Error PromotionPerCustomerLimitReached(int limit) =>
        Error.Conflict("Reservation.PromotionPerCustomerLimitReached",
            $"You have reached the redemption limit for this promotion code (maximum {limit} times).");
}
