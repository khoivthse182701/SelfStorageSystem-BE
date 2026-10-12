using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Admin;

public class AdminUserSummaryDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> AssignedFacilityNames { get; set; } = new();
    public List<long> AssignedFacilityIds { get; set; } = new();
}

public class AdminUserDetailDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<AdminUserRoleDto> Roles { get; set; } = new();
    public AdminCustomerProfileDto? CustomerProfile { get; set; }
    public AdminEmployeeProfileDto? EmployeeProfile { get; set; }
    public List<AdminLoginHistoryDto> RecentLoginHistories { get; set; } = new();
}

public class AdminUserRoleDto
{
    public short RoleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; }
    public long? GrantedBy { get; set; }
    public string? GrantedByEmail { get; set; }
}

public class AdminCustomerProfileDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? IdentityNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AdminEmployeeProfileDto
{
    public long UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly HireDate { get; set; }
    public string EmploymentStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<AdminStaffFacilityAssignmentDto> FacilityAssignments { get; set; } = new();
}

public class AdminCreateUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    public string? EmployeeCode { get; set; }

    public DateOnly? HireDate { get; set; }

    public long? InitialFacilityId { get; set; }
}

public class AdminUpdateUserStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public class AdminUpdateUserRolesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one role must be specified.")]
    public List<string> Roles { get; set; } = new();
}

public class AdminUserQueryParameters
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public long? FacilityId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
