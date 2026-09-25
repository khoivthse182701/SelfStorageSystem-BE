using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class StaffTask
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long? ShiftId { get; set; }

    public long? AssignedEmployeeId { get; set; }

    public long? TicketId { get; set; }

    public long? MaintenanceWorkOrderId { get; set; }

    public string TaskType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateTimeOffset? DueAt { get; set; }

    public string Status { get; set; } = null!;

    public short ProgressPercent { get; set; }

    public long CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual EmployeeProfile? AssignedEmployee { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;

    public virtual MaintenanceWorkOrder? MaintenanceWorkOrder { get; set; }

    public virtual StaffShift? Shift { get; set; }

    public virtual SupportTicket? Ticket { get; set; }
}
