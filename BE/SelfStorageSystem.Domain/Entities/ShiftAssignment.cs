using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class ShiftAssignment
{
    public long ShiftId { get; set; }

    public long EmployeeId { get; set; }

    public string DutyRole { get; set; } = null!;

    public DateTimeOffset? CheckInAt { get; set; }

    public DateTimeOffset? CheckOutAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual EmployeeProfile Employee { get; set; } = null!;

    public virtual StaffShift Shift { get; set; } = null!;
}
