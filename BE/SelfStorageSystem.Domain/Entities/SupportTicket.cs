using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SelfStorageSystem.Domain.Enums;

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

    [NotMapped, JsonIgnore]
    public TicketStatus TicketStatus
    {
        get => EnumDbMapper.ParseTicketStatus(Status);
        set => Status = value.ToDbValue();
    }

    public bool CanTransitionTo(TicketStatus nextStatus) =>
        EnumDbMapper.TryParseTicketStatus(Status, out var current) &&
        DomainStateMachine.CanTransition(current, nextStatus);

    public void TransitionStatus(TicketStatus nextStatus, DateTimeOffset? utcNow = null)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException($"Invalid ticket status transition from '{Status}' to '{nextStatus.ToDbValue()}'.");
        }

        var ts = utcNow ?? DateTimeOffset.UtcNow;
        Status = nextStatus.ToDbValue();
        if (nextStatus is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ResolvedAt ??= ts;
        }

        UpdatedAt = ts;
    }

    public virtual RentalAgreement? Agreement { get; set; }

    public virtual CustomerProfile Customer { get; set; } = null!;

    public virtual Facility Facility { get; set; } = null!;

    public virtual ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public virtual ServiceRating? ServiceRating { get; set; }

    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();

    public virtual StorageUnit? StorageUnit { get; set; }

    public virtual TicketAssignment? TicketAssignment { get; set; }

    public virtual ICollection<TicketAttachment> TicketAttachments { get; set; } = new List<TicketAttachment>();

    public virtual ICollection<TicketChargeProposal> TicketChargeProposals { get; set; } = new List<TicketChargeProposal>();

    public virtual ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();
}
