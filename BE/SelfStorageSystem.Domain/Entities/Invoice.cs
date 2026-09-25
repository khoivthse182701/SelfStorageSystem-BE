using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Invoice
{
    public long Id { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public long CustomerId { get; set; }

    public long? ReservationId { get; set; }

    public long? AgreementId { get; set; }

    public long? TicketChargeProposalId { get; set; }

    public string? BillingPeriod { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public string Currency { get; set; } = null!;

    public decimal SubtotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual DelinquencyCase? DelinquencyCase { get; set; }

    public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PromotionRedemption> PromotionRedemptions { get; set; } = new List<PromotionRedemption>();

    public virtual Reservation? Reservation { get; set; }

    public virtual TicketChargeProposal? TicketChargeProposal { get; set; }
}
