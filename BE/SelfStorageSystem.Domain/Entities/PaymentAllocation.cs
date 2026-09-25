using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class PaymentAllocation
{
    public long PaymentId { get; set; }

    public long InvoiceId { get; set; }

    public decimal AllocatedAmount { get; set; }

    public DateTimeOffset AllocatedAt { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Payment Payment { get; set; } = null!;
}
