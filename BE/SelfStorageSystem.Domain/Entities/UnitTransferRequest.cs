using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitTransferRequest
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long RequestedUnitTypeId { get; set; }

    public long FromUnitId { get; set; }

    public long? ToUnitId { get; set; }

    public DateOnly RequestedEffectiveDate { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long RequestedBy { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual StorageUnit FromUnit { get; set; } = null!;

    public virtual User RequestedByNavigation { get; set; } = null!;

    public virtual UnitType RequestedUnitType { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual StorageUnit? ToUnit { get; set; }
}
