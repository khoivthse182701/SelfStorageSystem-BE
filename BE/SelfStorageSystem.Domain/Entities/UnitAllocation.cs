using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class UnitAllocation
{
    public long Id { get; set; }

    public long StorageUnitId { get; set; }

    public long? ReservationId { get; set; }

    public long? AgreementId { get; set; }

    public string AllocationKind { get; set; } = null!;

    public DateOnly AllocationStartDate { get; set; }

    public DateOnly AllocationEndDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public long? AssignedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual ICollection<HandoverRecord> HandoverRecords { get; set; } = new List<HandoverRecord>();

    public virtual Reservation? Reservation { get; set; }

    public virtual StorageUnit StorageUnit { get; set; } = null!;
}
