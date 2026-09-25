using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class Inspection
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long? StorageUnitId { get; set; }

    public long? ReservationId { get; set; }

    public long? AgreementId { get; set; }

    public long InspectedBy { get; set; }

    public string InspectionType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? OverallCondition { get; set; }

    public string? Summary { get; set; }

    public DateTimeOffset? InspectedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<HandoverRecord> HandoverRecords { get; set; } = new List<HandoverRecord>();

    public virtual EmployeeProfile InspectedByNavigation { get; set; } = null!;

    public virtual ICollection<InspectionItem> InspectionItems { get; set; } = new List<InspectionItem>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual Reservation? Reservation { get; set; }

    public virtual StorageUnit? StorageUnit { get; set; }
}
