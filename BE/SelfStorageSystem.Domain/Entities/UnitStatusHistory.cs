using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitStatusHistory
{
    public long Id { get; set; }

    public long StorageUnitId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public long? ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public virtual User? ChangedByNavigation { get; set; }

    public virtual StorageUnit StorageUnit { get; set; } = null!;
}
