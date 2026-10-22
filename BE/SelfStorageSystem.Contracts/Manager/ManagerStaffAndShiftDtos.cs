using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerStaffShiftDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public long CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int AssignmentCount { get; set; }
}

public class ManagerCreateShiftRequest
{
    [Required]
    public string ShiftName { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset StartsAt { get; set; }

    [Required]
    public DateTimeOffset EndsAt { get; set; }

    public string? Status { get; set; }
}

public class ManagerShiftRosterItemDto
{
    public long ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DutyRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? CheckInAt { get; set; }
    public DateTimeOffset? CheckOutAt { get; set; }
}

public class ManagerCreateShiftAssignmentRequest
{
    [Required]
    public long ShiftId { get; set; }

    [Required]
    public long EmployeeId { get; set; }

    [Required]
    public string DutyRole { get; set; } = string.Empty; // staff, shift_lead, manager_on_call

    public string? Status { get; set; }
}

public class ManagerShiftAssignmentDto
{
    public long ShiftId { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DutyRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? CheckInAt { get; set; }
    public DateTimeOffset? CheckOutAt { get; set; }
}

public class ManagerCreateStaffTaskRequest
{
    public long? AssignedEmployeeId { get; set; }
    public long? ShiftId { get; set; }
    public long? TicketId { get; set; }
    public long? MaintenanceWorkOrderId { get; set; }

    [Required]
    public string TaskType { get; set; } = string.Empty; // check_in, check_out, inspection, maintenance, support, other

    [Required]
    public string Title { get; set; } = string.Empty;

    public DateTimeOffset? DueAt { get; set; }

    public string? Status { get; set; }

    public short? ProgressPercent { get; set; }
}

public class ManagerStaffTaskDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public long? ShiftId { get; set; }
    public long? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeCode { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public long? TicketId { get; set; }
    public long? MaintenanceWorkOrderId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? DueAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public short ProgressPercent { get; set; }
    public long CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}


public class ManagerFacilityStaffDto
{
    public long EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AssignmentRole { get; set; } = string.Empty;
    public string EmploymentStatus { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}
