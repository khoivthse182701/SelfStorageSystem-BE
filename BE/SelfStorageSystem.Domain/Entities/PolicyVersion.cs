using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class PolicyVersion
{
    public long Id { get; set; }

    public string PolicyType { get; set; } = null!;

    public string Version { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<RentalAgreement> RentalAgreements { get; set; } = new List<RentalAgreement>();
}
