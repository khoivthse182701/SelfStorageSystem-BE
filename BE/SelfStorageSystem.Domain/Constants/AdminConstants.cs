namespace SelfStorageSystem.Domain.Constants;

public static class AdminConstants
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public const string DefaultAuditSchema = "core";
    public const string DefaultEmployeeCodePrefix = "EMP-";

    public const int ConsecutiveFailedThreshold = 3;
    public const int SuspiciousWindowMinutes = 60;

    public const int MinPasswordLength = 6;
    public const int RecentLoginsTake = 20;
    public const string DefaultNotApplicable = "N/A";
    public const string RevokedStatus = "REVOKED";
    public const int AutoEmployeeCodeModulo = 1000000;

    public const string SuspiciousMultipleFailedReason = "Nhiều lần đăng nhập thất bại liên tiếp (>= 3 lần trong 60 phút)";
    public const string SuspiciousLockedAttemptReason = "Nỗ lực đăng nhập vào tài khoản đang bị khóa";
    public const string SuspiciousBlockedAttemptReason = "Đăng nhập bị chặn bởi hệ thống bảo mật";

    public static readonly string[] ValidUserStatuses = 
    { 
        UserStatusConstants.Active, 
        UserStatusConstants.Locked, 
        UserStatusConstants.Disabled 
    };

    public static readonly string[] ValidInternalRoles = 
    { 
        RoleConstants.FacilityStaff, 
        RoleConstants.FacilityManager, 
        RoleConstants.BusinessOperationsManager, 
        RoleConstants.SystemAdministrator 
    };

    public static readonly string[] ValidStaffAssignmentRoles = 
    { 
        StaffAssignmentRoles.FacilityStaff, 
        StaffAssignmentRoles.FacilityManager 
    };
}

public static class StaffAssignmentRoles
{
    public const string FacilityStaff = "facility_staff";
    public const string FacilityManager = "facility_manager";
}

public static class EmploymentStatusConstants
{
    public const string Active = "active";
    public const string Leave = "leave";
    public const string Terminated = "terminated";
}

public static class AuditActionConstants
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string UpdateStatus = "UPDATE_STATUS";
    public const string UpdateRoles = "UPDATE_ROLES";
    public const string AssignStaff = "ASSIGN_STAFF";
    public const string RevokeStaffAssignment = "REVOKE_STAFF_ASSIGNMENT";
}

public static class AuditEntityTypeConstants
{
    public const string User = "User";
    public const string StaffFacilityAssignment = "StaffFacilityAssignment";
    public const string StorageUnit = "StorageUnit";
    public const string Facility = "Facility";
}

public static class LoginHistoryResultConstants
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Locked = "locked";
    public const string Blocked = "blocked";

    public static readonly string[] All = { Succeeded, Failed, Locked, Blocked };
}


