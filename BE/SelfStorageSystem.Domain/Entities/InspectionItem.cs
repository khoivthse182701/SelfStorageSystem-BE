using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class InspectionItem
{
    public long Id { get; set; }

    public long InspectionId { get; set; }

    public string ItemName { get; set; } = null!;

    public string Condition { get; set; } = null!;

    public string? Notes { get; set; }

    public string? PhotoUrl { get; set; }

    public decimal ChargeAmount { get; set; }

    public virtual Inspection Inspection { get; set; } = null!;
}
