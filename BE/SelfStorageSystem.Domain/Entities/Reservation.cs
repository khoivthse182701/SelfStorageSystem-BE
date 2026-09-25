using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Reservation
{
    public long Id { get; set; }

    public string ReservationCode { get; set; } = null!;

    public long CustomerId { get; set; }

    public long FacilityId { get; set; }

    public long UnitTypeId { get; set; }

    public long FacilityRateId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal MonthlyRateSnapshot { get; set; }

    public decimal DepositSnapshot { get; set; }

    public decimal BookingFeeSnapshot { get; set; }

    public decimal DiscountSnapshot { get; set; }

    public decimal QuotedTotal { get; set; }

    public DateTimeOffset HoldUntil { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;

    public virtual FacilityRate FacilityRate { get; set; } = null!;

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<PromotionRedemption> PromotionRedemptions { get; set; } = new List<PromotionRedemption>();

    public virtual RentalAgreement? RentalAgreement { get; set; }

    public virtual UnitAllocation? UnitAllocation { get; set; }

    public virtual UnitType UnitType { get; set; } = null!;
}
