using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class StaffFacilityAssignment
{
    public long Id { get; set; }

    public long EmployeeId { get; set; }

    public long FacilityId { get; set; }

    public string AssignmentRole { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public long? AssignedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual EmployeeProfile Employee { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;
}
