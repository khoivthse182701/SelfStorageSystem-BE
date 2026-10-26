using SelfStorageSystem.Domain.Common;

namespace SelfStorageSystem.Domain.Errors;

public static class ManagerErrors
{
    public static readonly Error NotAssignedToFacility =
        Error.Forbidden("Manager.NotAssignedToFacility", "You do not have manager authority for this facility.");

    public static readonly Error FacilityNotFound =
        Error.NotFound("Manager.FacilityNotFound", "The target facility was not found.");

    public static readonly Error UnitNotFound =
        Error.NotFound("Manager.UnitNotFound", "Storage unit was not found.");

    public static readonly Error UnitCodeAlreadyExists =
        Error.Conflict("Manager.UnitCodeAlreadyExists", "A storage unit with this unit code already exists at this facility.");

    public static readonly Error UnitOccupiedCannotModify =
        Error.Conflict("Manager.UnitOccupiedCannotModify", "Cannot modify physical status directly while unit is currently occupied or reserved.");

    public static readonly Error AreaNotFound =
        Error.NotFound("Manager.AreaNotFound", "Facility area was not found or does not belong to this facility.");

    public static readonly Error UnitTypeNotFound =
        Error.NotFound("Manager.UnitTypeNotFound", "Storage unit type was not found.");

    public static readonly Error TransferRequestNotFound =
        Error.NotFound("Manager.TransferRequestNotFound", "Unit transfer request was not found.");

    public static readonly Error TransferRequestNotPending =
        Error.Conflict("Manager.TransferRequestNotPending", "Unit transfer request has already been reviewed or completed.");

    public static readonly Error ShiftNotFound =
        Error.NotFound("Manager.ShiftNotFound", "Staff shift was not found.");

    public static readonly Error InvalidShiftTimes =
        Error.Validation("Manager.InvalidShiftTimes", "Shift end time must be later than start time.");

    public static readonly Error ShiftAssignmentAlreadyExists =
        Error.Conflict("Manager.ShiftAssignmentAlreadyExists", "Employee is already assigned to this shift.");

    public static readonly Error PastShiftCreationNotAllowed =
        Error.Validation("Manager.PastShiftCreationNotAllowed", "Shift start time cannot be in the past without confirmation.");

    public static readonly Error PastShiftAssignmentNotAllowed =
        Error.Validation("Manager.PastShiftAssignmentNotAllowed", "Cannot assign employee to a shift in the past without confirmation.");

    public static readonly Error OverlappingShiftAssignment =
        Error.Conflict("Manager.OverlappingShiftAssignment", "Employee is already assigned to another shift during this time slot.");

    public static readonly Error DuplicateAssignmentInBatch =
        Error.Conflict("Manager.DuplicateAssignmentInBatch", "Batch contains duplicate assignments for the same employee and shift.");

    public static readonly Error EmployeeNotAssignedToFacility =
        Error.Validation("Manager.EmployeeNotAssignedToFacility", "The specified employee does not have an active assignment at this facility.");

    public static readonly Error InvalidTaskType =
        Error.Validation("Manager.InvalidTaskType", "Invalid task type specified.");

    public static readonly Error ChargeProposalNotFound =
        Error.NotFound("Manager.ChargeProposalNotFound", "Ticket charge proposal was not found.");

    public static readonly Error ChargeProposalAlreadyReviewed =
        Error.Conflict("Manager.ChargeProposalAlreadyReviewed", "Ticket charge proposal has already been decided.");

    public static readonly Error RefundNotFound =
        Error.NotFound("Manager.RefundNotFound", "Refund request was not found.");

    public static readonly Error RefundAlreadyReviewed =
        Error.Conflict("Manager.RefundAlreadyReviewed", "Refund request has already been reviewed.");

    public static readonly Error AgreementNotFound =
        Error.NotFound("Manager.AgreementNotFound", "Rental agreement was not found.");

    public static readonly Error DelinquencyCaseNotFound =
        Error.NotFound("Manager.DelinquencyCaseNotFound", "Delinquency case was not found.");

    public static readonly Error InvalidActionType =
        Error.Validation("Manager.InvalidActionType", "Invalid delinquency action type specified.");

    public static readonly Error InvalidDecision =
        Error.Validation("Manager.InvalidDecision", "Decision must be either 'approved' or 'rejected'.");

    public static readonly Error ManagerEmployeeProfileNotFound =
        Error.NotFound("Manager.EmployeeProfileNotFound", "The employee profile for the authenticated manager was not found.");
}
