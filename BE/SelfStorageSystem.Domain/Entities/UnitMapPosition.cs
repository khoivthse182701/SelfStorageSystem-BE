using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitMapPosition
{
    public long UnitId { get; set; }

    public long AreaId { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public decimal RotationDegrees { get; set; }

    public string Metadata { get; set; } = null!;

    public virtual FacilityArea Area { get; set; } = null!;

    public virtual StorageUnit Unit { get; set; } = null!;
}
