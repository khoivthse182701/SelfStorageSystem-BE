using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class FacilityRate
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long UnitTypeId { get; set; }

    public decimal MonthlyRate { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal BookingFee { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public long? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual UnitType UnitType { get; set; } = null!;
}
