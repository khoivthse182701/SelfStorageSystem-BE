using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class PriceRange
{
    public long Id { get; set; }

    public long UnitTypeId { get; set; }

    public decimal MinMonthlyRate { get; set; }

    public decimal MaxMonthlyRate { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual UnitType UnitType { get; set; } = null!;
}
