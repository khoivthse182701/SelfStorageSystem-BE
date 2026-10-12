using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class StaffErrors
{
    public static readonly Error EmployeeProfileNotFound =
        Error.NotFound("Staff.EmployeeProfileNotFound", "Employee profile was not found for the authenticated user.");

    public static readonly Error FacilityNotFound =
        Error.NotFound("Staff.FacilityNotFound", "Facility was not found.");

    public static readonly Error TaskNotFound =
        Error.NotFound("Staff.TaskNotFound", "Staff task was not found.");

    public static readonly Error ReservationNotFound =
        Error.NotFound("Staff.ReservationNotFound", "Reservation was not found.");

    public static readonly Error ReservationNotConfirmed =
        Error.Validation("Staff.ReservationNotConfirmed", "Reservation must be in confirmed status to perform check-in and unit assignment.");

    public static readonly Error UnitNotFound =
        Error.NotFound("Staff.UnitNotFound", "Storage unit was not found.");

    public static readonly Error UnitNotAvailable =
        Error.Conflict("Staff.UnitNotAvailable", "Storage unit is not available for assignment.");

    public static readonly Error UnitTypeMismatch =
        Error.Validation("Staff.UnitTypeMismatch", "Selected storage unit type does not match the reservation unit type.");

    public static readonly Error HandoverAlreadyExists =
        Error.Conflict("Staff.HandoverAlreadyExists", "Handover record has already been completed for this agreement/reservation.");

    public static readonly Error MoveOutNotFound =
        Error.NotFound("Staff.MoveOutNotFound", "Move-out request was not found.");

    public static readonly Error MoveOutNotSchedulable =
        Error.Validation("Staff.MoveOutNotSchedulable", "Move-out request is not in a valid status for inspection.");

    public static readonly Error ProposalNotFound =
        Error.NotFound("Staff.ProposalNotFound", "Ticket charge proposal was not found.");

    public static readonly Error ProposalAlreadyReviewed =
        Error.Conflict("Staff.ProposalAlreadyReviewed", "Ticket charge proposal has already been approved or rejected.");

    public static readonly Error InvalidStatus =
        Error.Validation("Staff.InvalidStatus", "Invalid status specified for this entity.");
}
