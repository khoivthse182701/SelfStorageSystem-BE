using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class PromotionRedemption
{
    public long Id { get; set; }

    public long PromotionId { get; set; }

    public long CustomerId { get; set; }

    public long? ReservationId { get; set; }

    public long? InvoiceId { get; set; }

    public decimal DiscountAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset RedeemedAt { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual Invoice? Invoice { get; set; }

    public virtual Promotion Promotion { get; set; } = null!;

    public virtual Reservation? Reservation { get; set; }
}
