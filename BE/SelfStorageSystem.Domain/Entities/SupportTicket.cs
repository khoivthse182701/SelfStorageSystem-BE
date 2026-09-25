using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class SupportTicket
{
    public long Id { get; set; }

    public string TicketNo { get; set; } = null!;

    public long CustomerId { get; set; }

    public long FacilityId { get; set; }

    public long? AgreementId { get; set; }

    public long? StorageUnitId { get; set; }

    public string Category { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Resolution { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual RentalAgreement? RentalAgreement { get; set; }

    public virtual ServiceRating? ServiceRating { get; set; }

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual StorageUnit? StorageUnit { get; set; }

    public virtual StorageUnit? StorageUnitNavigation { get; set; }

    public virtual TicketAssignment? TicketAssignment { get; set; }

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketChargeProposal> TicketChargeProposals { get; set; } = new List<TicketChargeProposal>();

    public virtual ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();
}
