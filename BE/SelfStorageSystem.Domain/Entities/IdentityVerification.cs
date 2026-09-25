using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class IdentityVerification
{
    public long Id { get; set; }

    public long ReservationId { get; set; }

    public long VerifiedBy { get; set; }

    public string VerificationMethod { get; set; } = null!;

    public string? DocumentFingerprint { get; set; }

    public string Result { get; set; } = null!;

    public string? Notes { get; set; }

    public DateTimeOffset VerifiedAt { get; set; }

    public virtual Reservation Reservation { get; set; } = null!;

    public virtual EmployeeProfile VerifiedByNavigation { get; set; } = null!;
}
