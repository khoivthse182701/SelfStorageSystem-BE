using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class TicketAssignment
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long EmployeeId { get; set; }

    public long? AssignedBy { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? EndReason { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual EmployeeProfile Employee { get; set; } = null!;

    public virtual SupportTicket Ticket { get; set; } = null!;
}
