using System;
using System.Collections.Generic;

namespace SelfStorageSystem.Domain.Entities;

public partial class TicketChargeProposal
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public long ProposedBy { get; set; }

    public string Description { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual EmployeeProfile ProposedByNavigation { get; set; } = null!;

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual TicketChargeApproval? TicketChargeApproval { get; set; }
}
