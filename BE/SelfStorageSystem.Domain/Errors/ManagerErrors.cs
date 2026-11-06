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

    public static readonly Error UnitCannotDeleteHasRentals =
        Error.Conflict("Manager.UnitCannotDeleteHasRentals", "Cannot delete storage unit with active rentals, reservations, or while occupied.");

    public static readonly Error UnitCannotDeleteHasHistory =
        Error.Conflict("Manager.UnitCannotDeleteHasHistory", "Cannot delete storage unit with historical tickets, inspections, maintenance orders, or transfer requests.");

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

    public static readonly Error RefundAlreadyCompleted =
        Error.Conflict("Manager.RefundAlreadyCompleted", "Khoản hoàn cọc này đã được xử lý hoàn tất trước đó.");

    public static readonly Error RefundMissingBankInfo =
        Error.Validation("Manager.RefundMissingBankInfo", "Khách hàng chưa cung cấp thông tin tài khoản ngân hàng để chuyển khoản.");

    public static readonly Error RefundAmountInvalid =
        Error.Validation("Manager.RefundAmountInvalid", "Số tiền hoàn cọc phải lớn hơn 0.");

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

    public static readonly Error AgreementNotActive =
        Error.Conflict("Manager.AgreementNotActive", "Only active or expiring soon agreements can have their storage unit reassigned.");

    public static readonly Error TargetUnitNotAvailable =
        Error.Conflict("Manager.TargetUnitNotAvailable", "The target storage unit is not available for assignment.");

    public static readonly Error TargetUnitDifferentFacility =
        Error.Conflict("Manager.TargetUnitDifferentFacility", "The target storage unit must be in the same facility as the rental agreement.");

    public static readonly Error DuplicateEmail =
        Error.Conflict("Manager.DuplicateEmail", "A user with this email address already exists.");

    public static readonly Error StaffNotFound =
        Error.NotFound("Manager.StaffNotFound", "Staff member was not found at this facility.");

    public static readonly Error TicketNotFound =
        Error.NotFound("Manager.TicketNotFound", "Support ticket was not found at this facility.");

    public static readonly Error StaffNotAssignedToFacility =
        Error.Validation("Manager.StaffNotAssignedToFacility", "The staff member is not assigned to this facility.");

    public static readonly Error InspectionNotFound =
        Error.NotFound("Manager.InspectionNotFound", "Move-out inspection was not found.");
}
