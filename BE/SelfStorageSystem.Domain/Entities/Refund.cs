using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Refund
{
    public long Id { get; set; }

    public long PaymentId { get; set; }

    public long? AgreementId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public string? ProviderRefundId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? RequestedBy { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual Payment Payment { get; set; } = null!;

    public virtual RefundApproval? RefundApproval { get; set; }

    public virtual User? RequestedByNavigation { get; set; }
}
