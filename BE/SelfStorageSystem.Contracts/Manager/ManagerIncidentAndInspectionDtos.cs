using System.ComponentModel.DataAnnotations;

namespace SelfStorageSystem.Contracts.Manager;

public class ManagerAssignTicketStaffRequest
{
    [Required]
    public long EmployeeId { get; set; }

    public long StaffId
    {
        get => EmployeeId;
        set => EmployeeId = value;
    }

    public long AssignedEmployeeId
    {
        get => EmployeeId;
        set => EmployeeId = value;
    }

    public string? Notes { get; set; }

    public DateTimeOffset? DueAt { get; set; }
}

public class ManagerCreateTicketRequest
{
    [Required]
    public long FacilityId { get; set; }
    public long? CustomerId { get; set; }
    public long? AgreementId { get; set; }
    public long? StorageUnitId { get; set; }
    public string Category { get; set; } = "incident";
    public string Priority { get; set; } = "normal";
    [Required]
    public string Subject { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    public long? AssignToEmployeeId { get; set; }
    public long? AssignToStaffId
    {
        get => AssignToEmployeeId;
        set => AssignToEmployeeId = value;
    }
}

public class ManagerTicketDetailDto
{
    public long Id { get; set; }
    public string TicketNo { get; set; } = string.Empty;
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public long? AgreementId { get; set; }
    public string? AgreementNo { get; set; }
    public long? StorageUnitId { get; set; }
    public string? UnitCode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public string? Resolution { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class ManagerAssignInspectionRequest
{
    [Required]
    public long AgreementId { get; set; }

    [Required]
    public long EmployeeId { get; set; }

    public long StaffId
    {
        get => EmployeeId;
        set => EmployeeId = value;
    }

    public long AssignedEmployeeId
    {
        get => EmployeeId;
        set => EmployeeId = value;
    }

    public long AssignedStaffId
    {
        get => EmployeeId;
        set => EmployeeId = value;
    }

    public string? Notes { get; set; }

    public DateTimeOffset? ScheduledAt { get; set; }
}

public class ManagerInspectionDetailDto
{
    public long Id { get; set; }
    public long FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public long AgreementId { get; set; }
    public string AgreementNo { get; set; } = string.Empty;
    public long? StorageUnitId { get; set; }
    public string? UnitCode { get; set; }
    public long InspectedBy { get; set; }
    public string InspectedByName { get; set; } = string.Empty;
    public string InspectionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OverallCondition { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
