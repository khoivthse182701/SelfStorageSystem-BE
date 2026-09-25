using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Payment
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public long TargetInvoiceId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Method { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string? ProviderTransactionId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset? PaidAt { get; set; }

    public string? FailureReason { get; set; }

    public string Metadata { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Invoice TargetInvoice { get; set; } = null!;
}
