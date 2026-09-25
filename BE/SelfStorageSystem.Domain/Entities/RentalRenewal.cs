using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class RentalRenewal
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long RequestedBy { get; set; }

    public DateOnly OldEndDate { get; set; }

    public DateOnly RequestedEndDate { get; set; }

    public DateOnly? ApprovedEndDate { get; set; }

    public decimal OldMonthlyRate { get; set; }

    public decimal? NewMonthlyRate { get; set; }

    public string Status { get; set; } = null!;

    public long? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual User RequestedByNavigation { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
