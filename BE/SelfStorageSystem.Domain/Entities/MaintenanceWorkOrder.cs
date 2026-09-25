using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class MaintenanceWorkOrder
{
    public long Id { get; set; }

    public string WorkOrderNo { get; set; } = null!;

    public long FacilityId { get; set; }

    public long? StorageUnitId { get; set; }

    public long? SourceTicketId { get; set; }

    public long? SourceInspectionId { get; set; }

    public long? AssignedEmployeeId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public bool BlocksBooking { get; set; }

    public string Status { get; set; } = null!;

    public decimal? EstimatedCost { get; set; }

    public decimal? ActualCost { get; set; }

    public long OpenedBy { get; set; }

    public long? VerifiedBy { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual EmployeeProfile? AssignedEmployee { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual User OpenedByNavigation { get; set; } = null!;

    public virtual Inspection? SourceInspection { get; set; }

    public virtual SupportTicket? SourceTicket { get; set; }

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual StorageUnit? StorageUnit { get; set; }

    public virtual EmployeeProfile? VerifiedByNavigation { get; set; }
}
