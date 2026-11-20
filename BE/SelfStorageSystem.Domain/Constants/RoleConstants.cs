namespace SelfStorageSystem.Domain.Constants;

public static class RoleConstants
{
    public const string Customer = "storage_customer";
    public const string FacilityStaff = "facility_staff";
    public const string FacilityManager = "facility_manager";
    public const string SystemAdministrator = "system_administrator";
    public const string SystemAdmin = "system_administrator";

    public const string Staff = "facility_staff";
    public const string Manager = "facility_manager";
    public const string Admin = "system_administrator";

    [Obsolete("Business operations manager role has been merged into facility_manager.")]
    public const string BusinessOperationsManager = "business_operations_manager";

    public const string StaffOrManager = "Staff,Manager,facility_staff,facility_manager";
    public const string StaffOrManagerOrAdmin = "Staff,Manager,Admin,facility_staff,facility_manager,system_administrator";
    public const string OperationsOrManager = "Manager,Admin,facility_manager,system_administrator";

    public const string CustomerDisplay = "Customer";
    public const string StaffDisplay = "Staff";
    public const string ManagerDisplay = "Manager";
    public const string AdminDisplay = "Admin";
}
