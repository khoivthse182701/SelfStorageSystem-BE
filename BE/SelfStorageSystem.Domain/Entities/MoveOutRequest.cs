using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class MoveOutRequest
{
    public long Id { get; set; }

    public long AgreementId { get; set; }

    public long RequestedBy { get; set; }

    public DateOnly RequestedMoveOutDate { get; set; }

    public long? AppointmentId { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTimeOffset? FinalizedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual RentalAgreement Agreement { get; set; } = null!;

    public virtual Appointment? Appointment { get; set; }

    public virtual User RequestedByNavigation { get; set; } = null!;
}
