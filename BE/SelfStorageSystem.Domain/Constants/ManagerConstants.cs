namespace SelfStorageSystem.Domain.Constants;

public static class ManagerConstants
{
    public const string ManagerOrAdmin = "facility_manager,system_administrator,business_operations_manager,manager,admin";

    public const string DefaultMapMetadata = "{}";
    public const string DefaultActionDetails = "{}";
    public const string DefaultInvoicePrefix = "INV-CHG-";
    public const int DefaultInvoiceDueDays = 3;
    public const int DelinquencyThresholdDays = 30;
}

public static class StaffShiftStatusConstants
{
    public const string Planned = "planned";
    public const string Open = "open";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = { Planned, Open, Completed, Cancelled };
}

public static class DutyRoleConstants
{
    public const string Staff = "staff";
    public const string ShiftLead = "shift_lead";
    public const string ManagerOnCall = "manager_on_call";

    public static readonly string[] All = { Staff, ShiftLead, ManagerOnCall };
}

public static class ShiftAssignmentStatusConstants
{
    public const string Scheduled = "scheduled";
    public const string CheckedIn = "checked_in";
    public const string CheckedOut = "checked_out";
    public const string Absent = "absent";

    public static readonly string[] All = { Scheduled, CheckedIn, CheckedOut, Absent };
}

public static class StaffTaskTypeConstants
{
    public const string CheckIn = "check_in";
    public const string CheckOut = "check_out";
    public const string Inspection = "inspection";
    public const string Maintenance = "maintenance";
    public const string Support = "support";
    public const string Other = "other";

    public static readonly string[] All = { CheckIn, CheckOut, Inspection, Maintenance, Support, Other };
}

public static class StaffTaskStatusConstants
{
    public const string Todo = "todo";
    public const string InProgress = "in_progress";
    public const string Blocked = "blocked";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = { Todo, InProgress, Blocked, Done, Cancelled };
}

public static class DelinquencyCaseStatusConstants
{
    public const string Grace = "grace";
    public const string Delinquent = "delinquent";
    public const string Cured = "cured";
    public const string Waived = "waived";
    public const string TerminationStarted = "termination_started";

    public static readonly string[] All = { Grace, Delinquent, Cured, Waived, TerminationStarted };
}

public static class DelinquencyActionTypeConstants
{
    public const string ReminderSent = "reminder_sent";
    public const string LateFeeAdded = "late_fee_added";
    public const string CredentialSuspended = "credential_suspended";
    public const string CustomerContacted = "customer_contacted";
    public const string PaymentPlan = "payment_plan";
    public const string Waived = "waived";
    public const string TerminationStarted = "termination_started";
    public const string Note = "note";

    public static readonly string[] All = 
    { 
        ReminderSent, LateFeeAdded, CredentialSuspended, 
        CustomerContacted, PaymentPlan, Waived, TerminationStarted, Note 
    };
}

public static class ApprovalDecisionConstants
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly string[] All = { Approved, Rejected };
}

public static class RefundStatusConstants
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Completed = "completed";

    public static readonly string[] All = { Pending, Approved, Rejected, Completed };
}

