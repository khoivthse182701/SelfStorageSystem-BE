using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class AdminErrors
{
    public static readonly Error UserNotFound =
        Error.NotFound("Admin.UserNotFound", "The specified user account was not found.");

    public static readonly Error EmailAlreadyExists =
        Error.Conflict("Admin.EmailAlreadyExists", "A user account with this email address already exists.");

    public static readonly Error CannotDeactivateSelf =
        Error.Validation("Admin.CannotDeactivateSelf", "Administrators cannot deactivate or lock their own account.");

    public static readonly Error CannotDemoteSelf =
        Error.Validation("Admin.CannotDemoteSelf", "Administrators cannot remove the system administrator role from their own account.");

    public static readonly Error InvalidRole =
        Error.Validation("Admin.InvalidRole", "One or more specified roles are invalid or not permitted for this operation.");

    public static readonly Error InvalidStatus =
        Error.Validation("Admin.InvalidStatus", "Invalid account status specified. Supported statuses are 'active', 'locked', 'disabled'.");

    public static readonly Error EmployeeProfileNotFound =
        Error.NotFound("Admin.EmployeeProfileNotFound", "The employee profile for the specified user was not found.");

    public static readonly Error FacilityNotFound =
        Error.NotFound("Admin.FacilityNotFound", "The target facility was not found.");

    public static readonly Error AssignmentNotFound =
        Error.NotFound("Admin.AssignmentNotFound", "The staff facility assignment record was not found.");

    public static readonly Error AssignmentAlreadyExists =
        Error.Conflict("Admin.AssignmentAlreadyExists", "The employee already has an active assignment for this facility.");

    public static readonly Error InvalidAssignmentRole =
        Error.Validation("Admin.InvalidAssignmentRole", "Invalid assignment role. Only 'facility_staff' or 'facility_manager' are allowed.");

    public static readonly Error InvalidAssignmentDates =
        Error.Validation("Admin.InvalidAssignmentDates", "The end date of the assignment must be after the start date.");

    public static readonly Error PasswordRequired =
        Error.Validation("Admin.PasswordRequired", "A strong password is required for internal account creation (min 6 characters).");
}
