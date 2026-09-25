using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Appointment
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long? ReservationId { get; set; }

    public long? AgreementId { get; set; }

    public long? AssignedStaffId { get; set; }

    public string AppointmentType { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string Status { get; set; } = null!;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual EmployeeProfile? AssignedStaff { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<MoveOutRequest> MoveOutRequests { get; set; } = new List<MoveOutRequest>();

    public virtual Reservation? Reservation { get; set; }
}
