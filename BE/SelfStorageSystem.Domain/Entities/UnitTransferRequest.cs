using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitTransferRequest
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long RequestedUnitTypeId { get; set; }

    public long RequestedFacilityId { get; set; }

    public long FromUnitId { get; set; }

    public long? ToUnitId { get; set; }

    public DateOnly RequestedEffectiveDate { get; set; }

    public DateOnly MoveOutDate { get; set; }

    public DateOnly MoveInDate { get; set; }

    public int RescheduleCount { get; set; }

    public decimal DiffRentAmount { get; set; }

    public decimal DiffDepositAmount { get; set; }

    public decimal PenaltyAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal TotalPaidAmount { get; set; }

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
