using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class DelinquencyCase
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long InvoiceId { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset GraceEndsAt { get; set; }

    public decimal OutstandingSnapshot { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? Notes { get; set; }

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual ICollection<DelinquencyAction> DelinquencyActions { get; set; } = new List<DelinquencyAction>();

    public virtual Invoice Invoice { get; set; } = null!;
}
