using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class FeeRule
{
    public long Id { get; set; }

    public long? FacilityId { get; set; }

    public string Code { get; set; } = null!;

    public string FeeType { get; set; } = null!;

    public string CalculationMethod { get; set; } = null!;

    public decimal? Amount { get; set; }

    public decimal? RatePercent { get; set; }

    public int GraceDays { get; set; }

    public string Conditions { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Facility? Facility { get; set; }
}
