using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Admin;

public class AdminStaffFacilityAssignmentDto
{
    public long Id { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public long FacilityId { get; set; }
    public string FacilityCode { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public string AssignmentRole { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public long? AssignedBy { get; set; }
    public string? AssignedByEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive => EndsAt == null || EndsAt > DateTimeOffset.UtcNow;
}

public class AdminCreateStaffAssignmentRequest
{
    [Required]
    public long EmployeeId { get; set; }

    [Required]
    public long FacilityId { get; set; }

    [Required]
    public string AssignmentRole { get; set; } = string.Empty;

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }
}
