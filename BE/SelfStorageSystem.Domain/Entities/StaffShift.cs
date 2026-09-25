using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class StaffShift
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public string ShiftName { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string Status { get; set; } = null!;

    public long CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
}
