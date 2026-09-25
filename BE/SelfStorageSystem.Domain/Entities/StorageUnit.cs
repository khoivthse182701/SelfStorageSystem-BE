using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class StorageUnit
{
    public long Id { get; set; }

    public long FacilityId { get; set; }

    public long UnitTypeId { get; set; }

    public long? AreaId { get; set; }

    public string UnitCode { get; set; } = null!;

    public string? FloorLabel { get; set; }

    public string? ZoneLabel { get; set; }

    public string PhysicalStatus { get; set; } = null!;

    public bool IsListed { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual FacilityArea? Area { get; set; }

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ICollection<SupportTicket> SupportTicketStorageUnitNavigations { get; set; } = new List<SupportTicket>();

    public virtual ICollection<SupportTicket> SupportTicketStorageUnits { get; set; } = new List<SupportTicket>();

    public virtual ICollection<UnitAllocation> UnitAllocations { get; set; } = new List<UnitAllocation>();

    public virtual UnitMapPosition? UnitMapPosition { get; set; }

    public virtual ICollection<UnitStatusHistory> UnitStatusHistories { get; set; } = new List<UnitStatusHistory>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestFromUnits { get; set; } = new List<UnitTransferRequest>();

    public virtual ICollection<UnitTransferRequest> UnitTransferRequestToUnits { get; set; } = new List<UnitTransferRequest>();

    public virtual UnitType UnitType { get; set; } = null!;
}
