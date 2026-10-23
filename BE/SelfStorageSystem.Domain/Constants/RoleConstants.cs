namespace SelfStorageSystem.Domain.Constants;

public static class RoleConstants
{
    public const string Customer = "storage_customer";
    public const string FacilityStaff = "facility_staff";
    public const string FacilityManager = "facility_manager";
    [Obsolete("Business operations manager role has been merged into facility_manager.")]
    public const string BusinessOperationsManager = "business_operations_manager";
    public const string SystemAdministrator = "system_administrator";
    public const string Staff = "facility_staff";
    public const string Manager = "facility_manager";
    public const string Admin = "system_administrator";

    // Operational staff, facility managers, and system administrators.
    public const string StaffOrManager = "facility_staff,facility_manager,system_administrator";
    public const string OperationsOrManager = FacilityManager + "," + SystemAdministrator;

    public const string CustomerDisplay = "Customer";
    public const string StaffDisplay = "Staff";
}
