using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerStaffItemDto
{
    public long UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly HireDate { get; set; }
    public string EmploymentStatus { get; set; } = string.Empty;
    public string UserStatus { get; set; } = string.Empty;
    public bool IsActive => UserStatus.Equals("active", StringComparison.OrdinalIgnoreCase);
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string AssignmentRole { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class ManagerCreateStaffRequest
{
    [Required]
    public long FacilityId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public string AssignmentRole { get; set; } = "facility_staff";

    public DateOnly? HireDate { get; set; }
}

public class ManagerUpdateStaffRequest
{
    [Required]
    [MinLength(2)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public DateOnly? HireDate { get; set; }

    public string? EmploymentStatus { get; set; }
}

public class ManagerUpdateStaffStatusRequest
{
    [Required]
    public string Status { get; set; } = "active"; // "active" | "inactive"
}

public class ManagerCustomerItemDto
{
    public long CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int TotalAgreements { get; set; }
    public int ActiveAgreements { get; set; }
    public int TotalReservations { get; set; }
    public DateTimeOffset FirstActivityAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
}
