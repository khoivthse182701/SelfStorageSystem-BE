using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Facility
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string? Ward { get; set; }

    public string? District { get; set; }

    public string City { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string Timezone { get; set; } = null!;

    public TimeOnly? OpeningTime { get; set; }

    public TimeOnly? ClosingTime { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual ICollection<AccessPoint> AccessPoints { get; set; } = new List<AccessPoint>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<FacilityArea> FacilityAreas { get; set; } = new List<FacilityArea>();

    public virtual ICollection<FacilityRate> FacilityRates { get; set; } = new List<FacilityRate>();

    public virtual ICollection<FeeRule> FeeRules { get; set; } = new List<FeeRule>();

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<RentalAgreement> RentalAgreements { get; set; } = new List<RentalAgreement>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual ICollection<StaffFacilityAssignment> StaffFacilityAssignments { get; set; } = new List<StaffFacilityAssignment>();

    public virtual ICollection<StaffShift> StaffShifts { get; set; } = new List<StaffShift>();

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual ICollection<StorageUnit> StorageUnits { get; set; } = new List<StorageUnit>();

    public virtual ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
}
